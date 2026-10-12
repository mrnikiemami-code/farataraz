using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.Ingestion.Contracts.Synchronization;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

namespace FaraTaraz.Modules.Ingestion.Application.Ports;

/// <summary>
/// Application-owned read port for the persisted synchronization checkpoint of one
/// source/capability (W3 architecture refactor, task M1).
///
/// It exposes only the opaque cursor token and its optimistic-concurrency version — no database
/// entity, no EF type. Like <see cref="IIngestionWriteStore"/> it takes no tenant argument: the
/// trusted tenant is resolved by the Infrastructure adapter from the trusted execution context,
/// so a caller-supplied tenant can never become an authorization authority.
/// </summary>
public interface IIngestionCheckpointStore
{
    /// <summary>
    /// Returns the current checkpoint for the trusted tenant's source/capability, or <c>null</c>
    /// when none has been stored yet (a fresh synchronization).
    /// </summary>
    Task<IngestionCheckpointRef?> ReadAsync(
        AccountingSourceId sourceId,
        string capability,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Snapshot of a persisted checkpoint: the opaque cursor token and the optimistic-concurrency
/// <paramref name="Version"/> the reader must carry into the next
/// <see cref="IIngestionWriteSession.SaveCheckpointAsync"/> call (CAS guard).
/// </summary>
/// <param name="CursorToken">Opaque continuation cursor token; interpreted only by the provider, never by the platform.</param>
/// <param name="Version">The DB-managed version observed at read time.</param>
/// <param name="State">Whether the checkpoint is a resumable continuation or a durably completed sync.</param>
public sealed record IngestionCheckpointRef(string CursorToken, long Version, SyncCheckpointState State);
