namespace FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Commands;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using MediatR;

/// <summary>
/// Application use case: run exactly ONE bounded customer synchronization step for one owned
/// accounting source under a trusted tenant. Synchronization mutates state (records, checkpoint and
/// run state), so it is a Command, not a Query.
///
/// The use case performs admission and orchestration; persistence is delegated to the inward
/// persistence ports. It never carries a <c>TenantId</c> derived from client input: the
/// <see cref="Tenant"/> is supplied by the trusted delivery boundary.
/// </summary>
public sealed record RunCustomerSyncCommand(
    TenantContext Tenant,
    SyncRequest Request) : IRequest<IngestionRunResult>;

/// <summary>Outcome of one bounded ingestion step.</summary>
/// <param name="RunId">The persisted synchronization run id.</param>
/// <param name="RecordsReceived">Records returned by the capability for this page.</param>
/// <param name="RecordsCreated">New source records actually stored (duplicates excluded).</param>
/// <param name="IsComplete">True when the page completed the synchronization.</param>
/// <param name="NextCursorToken">Continuation cursor token, or null when complete.</param>
public sealed record IngestionRunResult(
    long RunId,
    int RecordsReceived,
    long RecordsCreated,
    bool IsComplete,
    string? NextCursorToken);
