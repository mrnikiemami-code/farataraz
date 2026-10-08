namespace FaraTaraz.SyncContracts.Tests;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Provider-agnostic description of one capability's synchronization behavior, used by the
/// reusable conformance harness (<see cref="SyncConformance"/>).
///
/// This is NOT tied to the Mock: any adapter (Asan, Sepidar, …) can describe its capability
/// with the same shape and be tested against the identical behavioral contract.
/// </summary>
public sealed record SyncConformanceScenario<TRecord>(
    AccountingSourceId SourceId,
    string CapabilityName,
    SyncModeSupport ModeSupport,
    IReadOnlyList<TRecord> Dataset,
    int PageSize = 2)
{
    /// <summary>Optional: number of initial attempts that fail transiently before success.</summary>
    public int TransientFailuresBeforeSuccess { get; init; }

    /// <summary>
    /// Builds a <c>SyncRequest</c> for the given mode and optional resume cursor/batch size.
    /// The request carries only source + mode + cursor + batch limit; never a TenantId.
    /// </summary>
    public SyncRequest Request(SyncMode mode, SyncCursor? cursor = null, int? batchSize = null)
        => new(SourceId, mode, cursor, batchSize);
}

/// <summary>
/// Helpers to resolve a capability port from a provider using the real resolution path.
/// </summary>
public static class SyncConformancePorts
{
    /// <summary>
    /// Resolves the sync port for the declared capability, asserting support. This is the
    /// same path orchestration (W3) uses, so conformance exercises real resolution.
    /// </summary>
    public static ISyncablePort<TRecord> Resolve<TRecord>(
        this IAccountingProvider provider,
        AccountingCapability capability)
        where TRecord : notnull
        => provider.RequireCapability<ISyncablePort<TRecord>>(capability);
}
