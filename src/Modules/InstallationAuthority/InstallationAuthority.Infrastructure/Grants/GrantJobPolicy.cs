namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

/// <summary>
/// The single job-eligibility policy entrypoint: does signature-validated claims cover this exact
/// requested job at this evaluation time?
///
/// Order is fixed and cheap-before-expensive: issuer/audience identity, installation identity,
/// validity window, then exact source/capability membership. Membership is ordinal — there is no
/// normalization, case folding or prefix matching that could silently broaden scope.
///
/// <b>Evaluation time is not trusted offline time.</b> The caller supplies an instant so tests are
/// deterministic; a supplied or system clock is explicitly NOT a trusted clock and this type is
/// NOT a production authority issuer or an "offline verified" flag. Offline freshness/revocation
/// policy remains unresolved.
/// </summary>
internal static class GrantJobPolicy
{
    internal static GrantJobEligibilityFailure Evaluate(
        ValidatedGrantClaims claims,
        GrantJobRequest request,
        GrantAuthorityExpectations expectations,
        DateTimeOffset evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(expectations);

        if (!string.Equals(claims.Issuer, expectations.Issuer, StringComparison.Ordinal))
        {
            return GrantJobEligibilityFailure.IssuerMismatch;
        }

        if (!string.Equals(claims.Audience, expectations.Audience, StringComparison.Ordinal))
        {
            return GrantJobEligibilityFailure.AudienceMismatch;
        }

        if (!string.Equals(claims.InstallationId, request.ExpectedInstallationId, StringComparison.Ordinal))
        {
            return GrantJobEligibilityFailure.InstallationMismatch;
        }

        var now = evaluationTime.ToUnixTimeSeconds();

        if (now < claims.NotBeforeUnixSeconds)
        {
            return GrantJobEligibilityFailure.NotYetValid;
        }

        if (now >= claims.ExpiresAtUnixSeconds)
        {
            return GrantJobEligibilityFailure.Expired;
        }

        // Exact, ordinal membership: the granted source id and capability must match literally.
        for (var scopeIndex = 0; scopeIndex < claims.ScopeCount; scopeIndex++)
        {
            if (!string.Equals(claims.ScopeSourceId(scopeIndex).Value, request.SourceId.Value, StringComparison.Ordinal))
            {
                continue;
            }

            for (var capabilityIndex = 0; capabilityIndex < claims.CapabilityCount(scopeIndex); capabilityIndex++)
            {
                if (string.Equals(claims.Capability(scopeIndex, capabilityIndex), request.Capability, StringComparison.Ordinal))
                {
                    return GrantJobEligibilityFailure.None;
                }
            }
        }

        return GrantJobEligibilityFailure.ScopeNotGranted;
    }
}
