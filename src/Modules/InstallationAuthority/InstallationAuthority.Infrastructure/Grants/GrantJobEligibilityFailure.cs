namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Why a requested job is not covered by signature-validated claims. <c>None</c> means the claims
/// cover the job; it is still not authorization — possession must also be proven.
/// </summary>
internal enum GrantJobEligibilityFailure
{
    None = 0,

    /// <summary>The claims were not issued by the pinned company issuer.</summary>
    IssuerMismatch = 1,

    /// <summary>The claims are not addressed to the pinned platform audience.</summary>
    AudienceMismatch = 2,

    /// <summary>The claims belong to a different installation than the caller expects.</summary>
    InstallationMismatch = 3,

    /// <summary>The evaluation time is before the grant's not-before instant.</summary>
    NotYetValid = 4,

    /// <summary>The evaluation time is at or after the grant's expiry instant.</summary>
    Expired = 5,

    /// <summary>The requested source/capability is not granted (exact ordinal membership).</summary>
    ScopeNotGranted = 6,
}
