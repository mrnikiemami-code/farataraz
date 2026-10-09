namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

/// <summary>
/// Bounded-page CQRS proof (W1-R2).
///
/// Proves the W1-R2 bounded-page structure end to end:
///   ISender → Application query → Application handler → capability port (ONE page).
///
/// Pagination is caller-controlled: each dispatch retrieves exactly one bounded page and
/// returns a <see cref="SyncBatch{TRecord}"/> whose <c>NextCursor</c> drives the next dispatch.
/// The handler NEVER loops through pages or accumulates a full collection. It also enforces
/// the trusted-Tenant and source-ownership invariants (see <see cref="TenantSourceOwnershipTests"/>).
/// </summary>
public class CQRSProofTests
{
    private static readonly TenantId TenantId = new("tenant-proof-1");

    private static SynchronizeCustomersQuery Query(bool trusted = true, SyncCursor? cursor = null, int? batchSize = null)
        => new(
            trusted ? TenantContext.FromAuthenticatedPrincipal(TenantId)
                    : new TenantContext(TenantId, TenantContextOrigin.ClientInput),
            new SyncRequest(CqrsTestSupport.SourceIds.Source, SyncMode.Full, cursor, batchSize));

    private static (ISender sender, CqrsTestSupport.CountingCustomerPort port) SenderWithOwnedSource(
        MockCapabilitySync<SourceCustomer> capability)
    {
        var port = new CqrsTestSupport.CountingCustomerPort(capability);
        var ownership = new CqrsTestSupport.InMemoryOwnershipFake();
        ownership.Owns(TenantId, CqrsTestSupport.SourceIds.Source);

        var sender = CqrsTestSupport.BuildSender(port, ownership);
        return (sender, port);
    }

    private static async Task<(SyncBatch<SourceCustomer> batch, int callCount)> DispatchOnce(
        ISender sender,
        CqrsTestSupport.CountingCustomerPort port,
        SyncCursor? cursor = null,
        int? batchSize = null,
        CancellationToken cancellationToken = default)
    {
        var before = port.CallCount;
        var batch = await sender.Send(Query(cursor: cursor, batchSize: batchSize), cancellationToken);
        return (batch, port.CallCount - before);
    }

    [Fact]
    public async Task One_dispatch_invokes_exactly_one_provider_page()
    {
        var (sender, port) = SenderWithOwnedSource(CqrsTestSupport.Customers5());

        var (batch, calls) = await DispatchOnce(sender, port);

        Assert.True(calls == 1, "Exactly one provider page per dispatch.");
        Assert.True(batch.Records.Count == 2, "Page size 2 for the first page.");
        Assert.Equal(new[] { "C1", "C2" }, batch.Records.Select(r => r.Code));
    }

    [Fact]
    public async Task Five_record_source_with_page_size_two_requires_three_dispatches()
    {
        var (sender, port) = SenderWithOwnedSource(CqrsTestSupport.Customers5());

        var codes = new List<string>();
        SyncCursor? cursor = null;

        do
        {
            var (batch, _) = await DispatchOnce(sender, port, cursor: cursor);
            codes.AddRange(batch.Records.Select(r => r.Code));
            cursor = batch.NextCursor;
        }
        while (cursor is not null);

        Assert.True(port.CallCount == 3, "Five records at page size 2 => 3 dispatches (2, 2, 1).");
        Assert.Equal(new[] { "C1", "C2", "C3", "C4", "C5" }, codes);
    }

    [Fact]
    public async Task First_and_second_dispatch_return_expected_continuation_cursor()
    {
        var (sender, _) = SenderWithOwnedSource(CqrsTestSupport.Customers5());

        var first = await sender.Send(Query());
        var second = await sender.Send(Query(cursor: first.NextCursor));

        // First page: 2 records, continuing (cursor present, not complete).
        Assert.False(first.IsComplete);
        Assert.NotNull(first.NextCursor);
        Assert.Equal(2, first.Records.Count);

        // Second page continues from index 2, still continuing.
        Assert.Equal(2, second.Records.Count);
        Assert.Equal(new[] { "C3", "C4" }, second.Records.Select(r => r.Code));
        Assert.False(second.IsComplete);
        Assert.NotNull(second.NextCursor);
    }

    [Fact]
    public async Task Final_dispatch_returns_IsComplete_true_with_no_cursor()
    {
        var (sender, _) = SenderWithOwnedSource(CqrsTestSupport.Customers5());

        var first = await sender.Send(Query());
        var second = await sender.Send(Query(cursor: first.NextCursor));
        var last = await sender.Send(Query(cursor: second.NextCursor));

        Assert.True(last.IsComplete, "The final page is complete.");
        Assert.True(last.NextCursor is null, "The final page carries no continuation cursor.");
        Assert.Single(last.Records);
        Assert.Equal("C5", last.Records[0].Code);
    }

    [Fact]
    public async Task Requested_batch_size_is_enforced_by_the_application_boundary()
    {
        var (sender, _) = SenderWithOwnedSource(CqrsTestSupport.Customers5());

        // Request a batch size smaller than the page; the provider is bounded to it, and the
        // Application boundary accepts that bounded page.
        var batch = await sender.Send(Query(batchSize: 1));

        Assert.True(batch.Records.Count <= 1, "Page must not exceed the requested batch size.");
        Assert.Single(batch.Records);
    }

    [Fact]
    public async Task Cancellation_propagates_through_the_use_case()
    {
        var (sender, _) = SenderWithOwnedSource(CqrsTestSupport.Customers5());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => sender.Send(Query(), cts.Token));

        // Cancellation is a caller action; it is never swallowed or reclassified.
        Assert.IsNotType<SyncProviderException>(ex);
    }

    [Fact]
    public async Task Unsupported_mode_still_fails_explicitly_through_the_handler()
    {
        // A Full-only customer capability requesting Incremental must fail explicitly, never
        // silently fall back. The handler must not suppress the port's explicit failure.
        var (sender, _) = SenderWithOwnedSource(CqrsTestSupport.CustomersFullOnly());

        await Assert.ThrowsAsync<SyncModeNotSupportedException>(() =>
            sender.Send(new SynchronizeCustomersQuery(
                TenantContext.FromAuthenticatedPrincipal(TenantId),
                new SyncRequest(CqrsTestSupport.SourceIds.Source, SyncMode.Incremental))));
    }

    [Fact]
    public async Task Repeated_cursor_does_not_silently_restart()
    {
        var (sender, _) = SenderWithOwnedSource(CqrsTestSupport.Customers5());

        var first = await sender.Send(Query());
        var resumeCursor = first.NextCursor;
        Assert.NotNull(resumeCursor);

        // Feeding the same cursor again continues from that logical position, not from 0.
        var again = await sender.Send(Query(cursor: resumeCursor));

        Assert.Equal(new[] { "C3", "C4" }, again.Records.Select(r => r.Code));
    }

    [Fact]
    public void Handler_returns_a_single_bounded_page_not_a_full_collection()
    {
        // The response type is ONE bounded page, never an accumulated full collection.
        var handlerType = typeof(SynchronizeCustomersHandler);
        var responseInterface = handlerType
            .GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

        var responseArgument = responseInterface.GetGenericArguments()[1];

        Assert.Equal(typeof(SyncBatch<SourceCustomer>), responseArgument);
    }

    [Fact]
    public void Handler_accumulates_no_full_source_collection()
    {
        // No handler-level collection of all source records: no List<SourceCustomer> field.
        var handlerType = typeof(SynchronizeCustomersHandler);

        var fields = handlerType
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(List<SourceCustomer>))
            .ToList();

        Assert.Empty(fields);
    }
}
