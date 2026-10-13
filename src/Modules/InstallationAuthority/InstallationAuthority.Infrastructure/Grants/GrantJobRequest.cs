namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// The specific job a caller wants to run, together with the installation identity the caller
/// expects. It is an immutable internal value.
///
/// It carries <b>no tenant</b>: the tenant always comes from signature-validated claims, never
/// from the caller. It also carries no issuer/audience — those are pinned authority expectations,
/// never supplied per job.
///
/// Nonempty invariants are enforced at construction, so an invalid request cannot exist.
/// </summary>
internal sealed class GrantJobRequest
{
    internal GrantJobRequest(string expectedInstallationId, AccountingSourceId sourceId, string capability)
    {
        if (string.IsNullOrWhiteSpace(expectedInstallationId))
        {
            throw new ArgumentException("An expected installation id must be nonempty.", nameof(expectedInstallationId));
        }

        if (string.IsNullOrWhiteSpace(sourceId.Value))
        {
            throw new ArgumentException("A requested source id must be nonempty.", nameof(sourceId));
        }

        if (string.IsNullOrWhiteSpace(capability))
        {
            throw new ArgumentException("A requested capability must be nonempty.", nameof(capability));
        }

        ExpectedInstallationId = expectedInstallationId;
        SourceId = sourceId;
        Capability = capability;
    }

    internal string ExpectedInstallationId { get; }

    internal AccountingSourceId SourceId { get; }

    internal string Capability { get; }
}
