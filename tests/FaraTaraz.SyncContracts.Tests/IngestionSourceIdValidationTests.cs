namespace FaraTaraz.SyncContracts.Tests;

using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

public sealed class IngestionSourceIdValidationTests
{
    [Fact]
    public void Sync_request_rejects_empty_source_id()
    {
        Assert.Throws<InvalidSyncRequestException>(() =>
            new SyncRequest(new AccountingSourceId(" "), SyncMode.Full));
    }

    [Fact]
    public void Source_record_rejects_empty_source_id()
    {
        Assert.Throws<InvalidSourceRecordIdException>(() =>
            new SourceRecordId(new AccountingSourceId(" "), "Customer", "42"));
    }

    [Fact]
    public void Cursor_scope_rejects_blank_source_and_capability()
    {
        Assert.Throws<InvalidSyncCursorException>(() =>
            new SyncCursorScope(new AccountingSourceId(" "), "Customer"));
        Assert.Throws<InvalidSyncCursorException>(() =>
            new SyncCursorScope(new AccountingSourceId("source-a"), " "));
    }

    [Fact]
    public void Distinct_sources_remain_distinct()
    {
        Assert.NotEqual(
            new SourceRecordId(new AccountingSourceId("a"), "Customer", "42"),
            new SourceRecordId(new AccountingSourceId("b"), "Customer", "42"));
    }
}
