namespace FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Commands;

using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using MediatR;

/// <summary>Intent of a customer synchronization step (task W3-R3).</summary>
public enum CustomerSyncIntent
{
    /// <summary>
    /// Normal step: continue an <c>Active</c> checkpoint or begin a fresh first synchronization when
    /// none exists. A durably <c>Completed</c> checkpoint is REJECTED.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Deliberately start a NEW synchronization from the beginning. This is the ONLY intent that may
    /// pass a completed checkpoint; it must not supply a historical cursor.
    /// </summary>
    Restart = 1
}

/// <summary>
/// Application use case: run exactly ONE bounded customer synchronization step for one owned
/// accounting source under a trusted tenant. Synchronization mutates state (records, checkpoint and
/// run state), so it is a Command, not a Query.
///
/// The use case performs admission and orchestration; persistence is delegated to the inward
/// persistence ports. It never carries a <c>TenantId</c> derived from client input: the
/// <see cref="Tenant"/> is supplied by the trusted delivery boundary. <see cref="Intent"/> defaults
/// to <see cref="CustomerSyncIntent.Continue"/>; a completed synchronization requires an explicit
/// <see cref="CustomerSyncIntent.Restart"/>.
/// </summary>
public sealed record RunCustomerSyncCommand(
    TenantContext Tenant,
    SyncRequest Request,
    CustomerSyncIntent Intent = CustomerSyncIntent.Continue) : IRequest<IngestionRunResult>;

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
