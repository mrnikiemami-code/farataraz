namespace FaraTaraz.SyncContracts.Tests;

using System;
using System.Linq;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.Ingestion.Contracts.Synchronization;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

/// <summary>
/// Focused unit tests for the pure ingestion Domain policy extracted in Wave 2
/// (task <c>FT-REBASELINE-W2-DOMAIN-POLICY-001</c>).
///
/// These cover every branch of the four extracted rules (C5, C6, C8, C11) with no database,
/// provider, clock or DI — the policy is pure by construction. The behavioral parity of the full
/// use case remains proven by the unchanged real-PostgreSQL integration suite.
/// </summary>
public class IngestionSyncPolicyTests
{
    private static readonly AccountingSourceId Source = new("src-policy-1");
    private const string Capability = "Customers";

    private static SyncRequest Request(SyncCursor? cursor = null)
        => new(Source, SyncMode.Full, cursor);

    private static SyncCursor Cursor(string token, AccountingSourceId? source = null)
        => new(token, new SyncCursorScope(source ?? Source, Capability));

    // --- C5: an explicit restart must not supply a cursor ---------------------------------

    [Fact]
    public void Restart_with_a_caller_cursor_is_rejected()
        => Assert.Throws<InvalidSyncCursorException>(
            () => IngestionSyncPolicy.EnsureExplicitRestartHasNoCursor(
                isExplicitRestart: true, requestedCursor: Cursor("resume-1")));

    [Fact]
    public void Restart_without_a_cursor_is_allowed()
        => IngestionSyncPolicy.EnsureExplicitRestartHasNoCursor(
            isExplicitRestart: true, requestedCursor: null);

    [Fact]
    public void Continuation_with_a_cursor_is_allowed()
        => IngestionSyncPolicy.EnsureExplicitRestartHasNoCursor(
            isExplicitRestart: false, requestedCursor: Cursor("resume-1"));

    // --- C6: a Completed checkpoint is terminal unless an explicit restart -----------------

    [Fact]
    public void Completed_checkpoint_refuses_a_continuation_step()
    {
        var ex = Assert.Throws<SyncAlreadyCompletedException>(
            () => IngestionSyncPolicy.EnsureContinuationAllowed(
                isExplicitRestart: false,
                observedState: SyncCheckpointState.Completed,
                sourceId: Source));

        Assert.Equal(Source, ex.SourceId);
    }

    [Fact]
    public void Completed_checkpoint_is_passed_by_an_explicit_restart()
        => IngestionSyncPolicy.EnsureContinuationAllowed(
            isExplicitRestart: true,
            observedState: SyncCheckpointState.Completed,
            sourceId: Source);

    [Fact]
    public void Active_checkpoint_allows_a_continuation_step()
        => IngestionSyncPolicy.EnsureContinuationAllowed(
            isExplicitRestart: false,
            observedState: SyncCheckpointState.Active,
            sourceId: Source);

    [Fact]
    public void Active_checkpoint_allows_an_explicit_restart()
        => IngestionSyncPolicy.EnsureContinuationAllowed(
            isExplicitRestart: true,
            observedState: SyncCheckpointState.Active,
            sourceId: Source);

    [Fact]
    public void Absent_checkpoint_allows_a_first_step()
        => IngestionSyncPolicy.EnsureContinuationAllowed(
            isExplicitRestart: false,
            observedState: null,
            sourceId: Source);

    // --- C8: the caller cursor must agree with the persisted checkpoint cursor --------------

    [Fact]
    public void Persisted_cursor_is_restored_when_no_caller_cursor_is_supplied()
    {
        var resumed = IngestionSyncPolicy.ResumeFromPersistedCursor(
            Request(), persistedCursorToken: "persisted-7", capabilityName: Capability);

        Assert.NotNull(resumed.Cursor);
        Assert.Equal("persisted-7", resumed.Cursor!.Token);
        Assert.Equal(Source, resumed.Cursor.Scope.SourceId);
        Assert.Equal(Capability, resumed.Cursor.Scope.Capability);
        Assert.Equal(SyncMode.Full, resumed.Mode);
    }

    [Fact]
    public void Matching_caller_cursor_agrees_with_the_persisted_checkpoint()
    {
        var resumed = IngestionSyncPolicy.ResumeFromPersistedCursor(
            Request(Cursor("persisted-7")),
            persistedCursorToken: "persisted-7",
            capabilityName: Capability);

        Assert.Equal("persisted-7", resumed.Cursor!.Token);
    }

    [Fact]
    public void Conflicting_caller_cursor_is_rejected()
        => Assert.Throws<InvalidSyncCursorException>(
            () => IngestionSyncPolicy.ResumeFromPersistedCursor(
                Request(Cursor("caller-9")),
                persistedCursorToken: "persisted-7",
                capabilityName: Capability));

    // --- C11: identity exists -> equal (incl. null == null) is idempotent, else changed ------

    [Fact]
    public void Equal_non_null_fingerprint_is_an_idempotent_duplicate()
        => IngestionSyncPolicy.EnsureUnchangedOrIdempotent("fp-1", "fp-1", "C-1");

    [Fact]
    public void Null_fingerprint_matches_null_fingerprint()
        => IngestionSyncPolicy.EnsureUnchangedOrIdempotent(null, null, "C-1");

    [Fact]
    public void Stored_null_with_non_null_incoming_is_a_changed_record()
    {
        var ex = Assert.Throws<UnsupportedChangedRecordException>(
            () => IngestionSyncPolicy.EnsureUnchangedOrIdempotent(null, "fp-1", "C-1"));

        Assert.Equal("C-1", ex.ExternalId);
    }

    [Fact]
    public void Stored_non_null_with_null_incoming_is_a_changed_record()
        => Assert.Throws<UnsupportedChangedRecordException>(
            () => IngestionSyncPolicy.EnsureUnchangedOrIdempotent("fp-1", null, "C-1"));

    [Fact]
    public void Different_non_null_fingerprints_are_a_changed_record()
        => Assert.Throws<UnsupportedChangedRecordException>(
            () => IngestionSyncPolicy.EnsureUnchangedOrIdempotent("fp-1", "fp-2", "C-1"));

    // --- Purity: the real Domain assembly stays inward and provider/MediatR/EF-free ---------

    [Fact]
    public void Ingestion_Domain_assembly_references_only_inward_projects_and_no_persistence_or_mediatr()
    {
        var domain = typeof(SyncCheckpointState).Assembly;

        Assert.Equal("FaraTaraz.Modules.Ingestion.Domain", domain.GetName().Name);

        var references = domain
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        Assert.DoesNotContain(references, n => n.StartsWith("MediatR", StringComparison.Ordinal));
        Assert.DoesNotContain(references, n => n.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, n => n.Contains("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, n => n.Contains("Adapters", StringComparison.Ordinal));

        var faraTarazReferences = references.Where(n => n.StartsWith("FaraTaraz", StringComparison.Ordinal));
        Assert.All(faraTarazReferences, n => Assert.Contains(n, new[]
        {
            "FaraTaraz.BuildingBlocks",
            "FaraTaraz.Modules.MasterData",
            "FaraTaraz.Modules.Ingestion.Contracts",
        }));

        // The reverse edge is forbidden: Contracts must not depend on Domain.
        var contractsReferences = typeof(SyncRequest).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();
        Assert.DoesNotContain("FaraTaraz.Modules.Ingestion.Domain", contractsReferences);
    }
}
