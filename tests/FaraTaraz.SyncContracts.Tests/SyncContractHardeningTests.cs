namespace FaraTaraz.SyncContracts.Tests;

using System.Threading;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

/// <summary>
/// Hardening evidence for the W1-R2 sync contract invariants (task §6):
///   - SyncRequest.BatchSize rejects zero/negative values;
///   - SourceRecordId rejects empty/whitespace record kind / external id;
///   - SyncCursor rejects unreasonably large tokens (opaque semantics preserved);
///   - the raw cursor token is never exposed by SyncCursor.ToString().
/// </summary>
public class SyncContractHardeningTests
{
    private static readonly AccountingSourceId Source = SourceIds.Source;

    [Fact]
    public void SyncRequest_rejects_zero_batch_size()
    {
        Assert.Throws<InvalidSyncRequestException>(
            () => new SyncRequest(Source, SyncMode.Full, batchSize: 0));
    }

    [Fact]
    public void SyncRequest_rejects_negative_batch_size()
    {
        Assert.Throws<InvalidSyncRequestException>(
            () => new SyncRequest(Source, SyncMode.Full, batchSize: -3));
    }

    [Fact]
    public void SyncRequest_accepts_positive_and_absent_batch_size()
    {
        var withSize = new SyncRequest(Source, SyncMode.Full, batchSize: 4);
        Assert.Equal(4, withSize.BatchSize);

        var absent = new SyncRequest(Source, SyncMode.Full);
        Assert.Null(absent.BatchSize);
    }

    [Fact]
    public void SourceRecordId_rejects_whitespace_record_kind()
    {
        Assert.Throws<InvalidSourceRecordIdException>(
            () => new SourceRecordId(Source, "  ", "C1"));
    }

    [Fact]
    public void SourceRecordId_rejects_whitespace_external_id()
    {
        Assert.Throws<InvalidSourceRecordIdException>(
            () => new SourceRecordId(Source, "Customer", "\t"));
    }

    [Fact]
    public void SourceRecordId_accepts_nonempty_identity()
    {
        var id = new SourceRecordId(Source, "Customer", "C1");
        Assert.Equal("Customer", id.RecordKind);
        Assert.Equal("C1", id.ExternalId);
    }

    [Fact]
    public void SyncCursor_rejects_oversized_token()
    {
        var longToken = new string('x', CqrsHardening.LongToken.Length);
        Assert.Throws<InvalidSyncCursorException>(
            () => new SyncCursor(longToken, new SyncCursorScope(Source, "Customers")));
    }

    [Fact]
    public void SyncCursor_accepts_a_reasonably_large_token()
    {
        var token = new string('x', CqrsHardening.OkToken.Length);
        var cursor = new SyncCursor(token, new SyncCursorScope(Source, "Customers"));
        Assert.Equal(token, cursor.Token);
    }

    [Fact]
    public void SyncCursor_does_not_expose_the_raw_token_in_ToString()
    {
        var cursor = new SyncCursor("super-secret-page-token", new SyncCursorScope(Source, "Customers"));

        var rendered = cursor.ToString();

        Assert.DoesNotContain("super-secret-page-token", rendered);
        Assert.Contains("Customers", rendered);
    }
}

/// <summary>Token fixtures for the cursor length hardening test.</summary>
internal static class CqrsHardening
{
    // Just under the SyncCursor.MaxTokenLength ceiling (1024).
    public static readonly string OkToken = new('x', 512);

    // Just over the ceiling.
    public static readonly string LongToken = new('x', 1025);
}
