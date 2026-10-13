namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// Pinned authority expectations the claims must match exactly: the company issuer and the
/// platform audience. They are configuration of the authority, never supplied by a job payload,
/// and never taken from the grant itself.
///
/// Provisioning the real production values remains an explicit open dependency: this type exists
/// so the policy takes the expectation as an input instead of hard-coding or inferring it.
/// </summary>
internal sealed class GrantAuthorityExpectations
{
    internal GrantAuthorityExpectations(string issuer, string audience)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new ArgumentException("An expected issuer must be nonempty.", nameof(issuer));
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new ArgumentException("An expected audience must be nonempty.", nameof(audience));
        }

        Issuer = issuer;
        Audience = audience;
    }

    internal string Issuer { get; }

    internal string Audience { get; }
}
