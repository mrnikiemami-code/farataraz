namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Owns the atomic unit of work for one synchronization step over the Ingestion database
/// (FT-DATA-002; ADR-010 decision 8).
///
/// A synchronization step produces several related writes — a run row, source-record rows and a
/// checkpoint/cursor advancement — that MUST commit together or not at all: a checkpoint that
/// advanced past source records which did not commit would permanently lose data. The run,
/// source-record and checkpoint repositories are all constructed over the single scoped
/// <see cref="IngestionDbContext"/>, so one explicit database transaction on that context spans
/// every write. This type commits only after the work completes and rolls the whole step back on
/// any exception (including a stale-checkpoint CAS rejection) or cancellation before commit, so a
/// prematurely advanced cursor is impossible.
///
/// The ownership oracle lives in a different module database and is therefore NOT part of this
/// write unit; it is consulted (read-only, fail-closed) before the unit of work begins.
/// </summary>
public sealed class IngestionUnitOfWork
{
    private readonly IngestionDbContext _db;

    /// <summary>Wraps the module unit of work (the scoped <see cref="IngestionDbContext"/>).</summary>
    public IngestionUnitOfWork(IngestionDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>
    /// Runs <paramref name="work"/> inside a single database transaction and commits only if it
    /// completes successfully. If <paramref name="work"/> throws — a domain error, a persistence
    /// failure, a stale-checkpoint CAS rejection, or cancellation — control never reaches the
    /// commit and the transaction is rolled back on disposal, so no partial write survives.
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await work(cancellationToken).ConfigureAwait(false);

        // Reached only when every required write succeeded. Cancellation before the commit, or any
        // exception above, skips this and the `await using` disposal rolls the transaction back.
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
