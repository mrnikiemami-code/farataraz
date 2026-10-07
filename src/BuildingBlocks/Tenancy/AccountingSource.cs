namespace FaraTaraz.BuildingBlocks.Tenancy;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// One configured accounting-system instance that belongs to exactly one <see cref="Tenant"/>.
///
/// A Tenant may own many AccountingSources (e.g. three Asan branches, or a mix of Asan + Sepidar).
/// One provider adapter may serve many AccountingSources.
///
/// <see cref="TenantId"/> is a required constructor argument: an AccountingSource
/// cannot exist without a tenant. This is the type-level guarantee of tenant ownership.
/// </summary>
public sealed class AccountingSource
{
    public AccountingSource(
        AccountingSourceId id,
        TenantId tenantId,
        ProviderId provider,
        string displayName,
        AccountingSourceStatus status)
    {
        Id = id;
        // Required argument -> cannot be constructed without a tenant.
        TenantId = tenantId;
        Provider = provider;
        DisplayName = displayName;
        Status = status;
    }

    /// <summary>Platform-owned identity of this accounting source.</summary>
    public AccountingSourceId Id { get; }

    /// <summary>The single tenant this source belongs to. Never empty.</summary>
    public TenantId TenantId { get; }

    /// <summary>The provider identity (e.g. Asan / Sepidar / Mock). Not a provider DTO.</summary>
    public ProviderId Provider { get; }

    /// <summary>Human-readable name used by operators.</summary>
    public string DisplayName { get; }

    /// <summary>Operational status of the source.</summary>
    public AccountingSourceStatus Status { get; }

    public static AccountingSource Create(
        AccountingSourceId id,
        TenantId tenantId,
        ProviderId provider,
        string displayName,
        AccountingSourceStatus status)
        => new(id, tenantId, provider, displayName, status);
}

public enum AccountingSourceStatus
{
    Disabled = 0,
    Enabled = 1,
    Syncing = 2,
    Error = 3
}
