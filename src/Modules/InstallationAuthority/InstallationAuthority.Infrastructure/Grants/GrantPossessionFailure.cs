namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>Why a proof of possession did not succeed. Failure is never a positive result.</summary>
internal enum GrantPossessionFailure
{
    None = 0,

    /// <summary>The one-shot session was already consumed by an earlier completion attempt.</summary>
    SessionConsumed = 1,

    /// <summary>The supplied signature was absent or not exactly 64 bytes.</summary>
    MalformedSignature = 2,

    /// <summary>The signature did not verify for this session's message and installation key.</summary>
    InvalidSignature = 3,
}
