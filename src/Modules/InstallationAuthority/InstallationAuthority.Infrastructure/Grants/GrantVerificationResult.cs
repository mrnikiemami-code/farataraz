namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Stage-A verification result: either immutable signature-validated claims or a failure reason.
///
/// It is internal and exposes no public "already verified" assertion API, and it carries no
/// tenant authority: obtaining <see cref="Claims"/> does not authorize anything.
/// </summary>
internal sealed class GrantVerificationResult
{
    private readonly ValidatedGrantClaims? _claims;

    private GrantVerificationResult(ValidatedGrantClaims? claims, GrantVerificationFailure failure)
    {
        _claims = claims;
        Failure = failure;
    }

    /// <summary>True only when the signature verified and the claims were structurally valid.</summary>
    internal bool IsValid => Failure == GrantVerificationFailure.None;

    /// <summary>The verification failure reason; <c>None</c> when valid.</summary>
    internal GrantVerificationFailure Failure { get; }

    /// <summary>
    /// The signature-validated claims. Throws when the result is not valid, so a rejection can
    /// never be read as a success by accident.
    /// </summary>
    internal ValidatedGrantClaims Claims =>
        _claims ?? throw new InvalidOperationException(
            $"Grant verification failed ({Failure}); there are no validated claims.");

    internal static GrantVerificationResult Valid(ValidatedGrantClaims claims)
        => new(claims ?? throw new ArgumentNullException(nameof(claims)), GrantVerificationFailure.None);

    internal static GrantVerificationResult Rejected(GrantVerificationFailure failure)
        => failure == GrantVerificationFailure.None
            ? throw new ArgumentOutOfRangeException(nameof(failure), "A rejection must carry a failure reason.")
            : new GrantVerificationResult(null, failure);
}
