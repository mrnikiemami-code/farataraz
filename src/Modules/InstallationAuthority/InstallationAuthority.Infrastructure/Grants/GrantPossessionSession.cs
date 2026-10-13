namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using System.Security.Cryptography;

/// <summary>
/// A one-shot proof-of-possession session for one exact job. It may be created only after
/// eligibility checks pass (<see cref="GrantJobPolicy"/>).
///
/// The challenge nonce (32 random bytes / 256 bits; at least 128 required) is generated internally
/// with <see cref="RandomNumberGenerator"/> and is never a caller input. The installation must sign
/// the canonical message from <see cref="PossessionMessage"/>, which binds the protocol identifier,
/// grant id, tenant id, installation id, source id, capability and nonce.
///
/// Verification uses the installation SubjectPublicKeyInfo taken from the accepted claims — never a
/// key supplied with the response — requires named P-256 identity and an exactly 64-byte
/// ES256/P1363 signature, and enforces complete SPKI consumption.
///
/// The session is single-use and atomic: one completion attempt is accepted, a failed attempt also
/// consumes it, and a proof produced for another session/job/installation cannot verify because the
/// message (including the fresh nonce) differs. Cancellation propagates and can never yield a
/// successful result. No signer is implemented here: a future OS signer consumes exactly these
/// challenge bytes.
/// </summary>
internal sealed class GrantPossessionSession
{
    private const int ChallengeBytes = 32;   // 256 random bits
    private const int SignatureBytes = 64;   // ES256 P1363 r||s

    private readonly byte[] _nonce;
    private readonly byte[] _message;
    private readonly byte[] _installationSpki;
    private readonly ValidatedGrantClaims _claims;
    private readonly GrantJobRequest _request;
    private int _consumed;

    private GrantPossessionSession(
        ValidatedGrantClaims claims, GrantJobRequest request, byte[] nonce, byte[] message, byte[] installationSpki)
    {
        _claims = claims;
        _request = request;
        _nonce = nonce;
        _message = message;
        _installationSpki = installationSpki;
    }

    /// <summary>
    /// Starts a session for an eligible job: generates the fresh challenge and binds the canonical
    /// message to that exact job.
    /// </summary>
    internal static GrantPossessionSession Begin(
        ValidatedGrantClaims claims, GrantJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var nonce = new byte[ChallengeBytes];
        RandomNumberGenerator.Fill(nonce);

        var message = PossessionMessage.Encode(
            claims.GrantId,
            claims.TenantId.Value,
            claims.InstallationId,
            request.SourceId.Value,
            request.Capability,
            nonce);

        // The installation key is a private copy taken from the accepted claims.
        return new GrantPossessionSession(claims, request, nonce, message, claims.CopyInstallationPublicKeySpki());
    }

    /// <summary>A fresh copy of the challenge bytes, for handing to the installation key signer.</summary>
    internal byte[] CopyChallenge() => (byte[])_nonce.Clone();

    /// <summary>A fresh copy of the canonical message the installation must sign.</summary>
    internal byte[] CopySignedMessage() => (byte[])_message.Clone();

    /// <summary>
    /// Makes the single completion attempt. The session is consumed whether the proof succeeds or
    /// fails; later attempts report <see cref="GrantPossessionFailure.SessionConsumed"/>.
    /// </summary>
    internal GrantPossessionResult Complete(byte[] signature, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _consumed, 1, 0) != 0)
        {
            return GrantPossessionResult.Failed(GrantPossessionFailure.SessionConsumed);
        }

        if (signature is null || signature.Length != SignatureBytes)
        {
            return GrantPossessionResult.Failed(GrantPossessionFailure.MalformedSignature);
        }

        bool verified;

        try
        {
            using var installationKey = ECDsa.Create();
            installationKey.ImportSubjectPublicKeyInfo(_installationSpki, out var read);

            if (read != _installationSpki.Length || !P256Curve.IsP256(installationKey))
            {
                return GrantPossessionResult.Failed(GrantPossessionFailure.InvalidSignature);
            }

            verified = installationKey.VerifyData(
                _message, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return GrantPossessionResult.Failed(GrantPossessionFailure.InvalidSignature);
        }

        if (!verified)
        {
            return GrantPossessionResult.Failed(GrantPossessionFailure.InvalidSignature);
        }

        // A cancelled caller must never observe a successful proof result.
        cancellationToken.ThrowIfCancellationRequested();

        return GrantPossessionResult.Succeeded(
            _claims.TenantId, _claims.GrantId, _claims.InstallationId, _request.SourceId, _request.Capability);
    }
}
