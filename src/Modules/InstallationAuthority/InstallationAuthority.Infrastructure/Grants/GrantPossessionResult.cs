namespace FaraTaraz.Modules.InstallationAuthority.Infrastructure.Grants;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// The outcome of one possession attempt, bound to the exact job it was proven for.
///
/// <b>This is NOT tenant authority.</b> It is not <c>AuthorizedJobGrant</c>, it cannot mint
/// <c>TenantContext</c> or <c>DatabaseTenantScope</c>, and a successful proof is explicitly NOT
/// complete offline authorization: offline freshness/revocation and restricted trust binding
/// remain unresolved. The tenant value here is the signature-validated claim value, not a new
/// authority.
/// </summary>
internal sealed class GrantPossessionResult
{
    private readonly string _grantId;
    private readonly string _installationId;
    private readonly string _capability;

    private GrantPossessionResult(
        GrantPossessionFailure failure,
        TenantId tenantId,
        string grantId,
        string installationId,
        AccountingSourceId sourceId,
        string capability)
    {
        Failure = failure;
        TenantId = tenantId;
        _grantId = grantId;
        _installationId = installationId;
        SourceId = sourceId;
        _capability = capability;
    }

    internal bool IsSucceeded => Failure == GrantPossessionFailure.None;

    internal GrantPossessionFailure Failure { get; }

    internal TenantId TenantId { get; }

    internal AccountingSourceId SourceId { get; }

    internal string GrantId => IsSucceeded ? _grantId : throw new InvalidOperationException(NotSucceeded);

    internal string InstallationId => IsSucceeded ? _installationId : throw new InvalidOperationException(NotSucceeded);

    internal string Capability => IsSucceeded ? _capability : throw new InvalidOperationException(NotSucceeded);

    private const string NotSucceeded = "The possession attempt did not succeed; it carries no bound job.";

    internal static GrantPossessionResult Succeeded(
        TenantId tenantId, string grantId, string installationId, AccountingSourceId sourceId, string capability)
        => new(GrantPossessionFailure.None, tenantId, grantId, installationId, sourceId, capability);

    internal static GrantPossessionResult Failed(GrantPossessionFailure failure)
        => failure == GrantPossessionFailure.None
            ? throw new ArgumentOutOfRangeException(nameof(failure), "A failure result must carry a reason.")
            : new(failure, default, string.Empty, string.Empty, default, string.Empty);
}
