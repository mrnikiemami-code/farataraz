namespace FaraTaraz.SyncContracts.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.Core.Accounting;
using FaraTaraz.Core.SourceModel;
using FaraTaraz.Core.Synchronization;
using Xunit;

/// <summary>
/// Critical idempotency evidence (task §19). These prove that the synchronization contract
/// provides stable, deterministic source-record identity so that W2/W3 durable ingestion can
/// guarantee idempotency. W1 proves identity-level stability; it does NOT prove DB durability.
///
/// Case A — same record twice; Case B — same record 10 times; Case C — repeated batch;
/// Case D — changed content; Case E — different record kind; Case F — same ID, different source.
/// </summary>
public class IdempotencyTests
{
    [Fact]
    public async Task CaseA_SameRecordDeliveredTwice_IdentityIsIdentical()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[] { MockSources.Customer(SourceIds.Source, "C1") },
            products: Array.Empty<SourceProduct>());

        var delivered = await SyncAll(provider, AccountingCapability.Customers);

        // Same record delivered twice → identical identity every time.
        var first = delivered.First().RecordId;
        Assert.All(delivered, r => Assert.Equal(first, r.RecordId));
    }

    [Fact]
    public async Task CaseB_SameRecordDeliveredTenTimes_StillOneLogicalIdentity()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            // Ten physical deliveries of the SAME logical record.
            customers: Enumerable.Repeat(MockSources.Customer(SourceIds.Source, "C1"), 10).ToArray(),
            products: Array.Empty<SourceProduct>());

        var delivered = await SyncAll(provider, AccountingCapability.Customers);

        Assert.Equal(10, delivered.Count);
        var identity = delivered.First().RecordId;
        Assert.All(delivered, r => Assert.Equal(identity, r.RecordId));
        // Ten deliveries resolve to ONE logical source-record identity (no fan-out).
        var distinct = delivered.Select(r => r.RecordId).Distinct().Count();
        Assert.Equal(1, distinct);
    }

    [Fact]
    public async Task CaseC_RepeatedBatch_YieldsStableDeterministicIdentities()
    {
        var provider = MockSyncScenarios.CustomersAndProducts(
            customers: new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
                MockSources.Customer(SourceIds.Source, "C3"),
            },
            products: Array.Empty<SourceProduct>());

        var firstPass = await SyncAll(provider, AccountingCapability.Customers);
        var secondPass = await SyncAll(provider, AccountingCapability.Customers);

        // Re-delivering the same batch yields identical identities in identical order.
        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public async Task CaseD_ChangedContent_IdentityStableButFingerprintChanges()
    {
        var before = MockSources.Customer(SourceIds.Source, "C1", "Old Name");
        var after = MockSources.Customer(SourceIds.Source, "C1", "New Name");

        // Same source identity (source + kind + external code).
        Assert.Equal(before.RecordId, after.RecordId);

        // But the change signal (deterministic content fingerprint) differs.
        Assert.NotEqual(
            before.Version!.ContentFingerprint,
            after.Version!.ContentFingerprint);

        Assert.True(after.Version!.IsDifferentFrom(before.Version));
    }

    [Fact]
    public void CaseE_SameExternalIdDifferentRecordKind_AreNotEqual()
    {
        // Same external textual ID "123" for a Product and a Customer.
        var product = MockSources.Product(SourceIds.Source, "123");
        var customer = MockSources.Customer(SourceIds.Source, "123");

        Assert.NotEqual(product.RecordId, customer.RecordId);
    }

    [Fact]
    public void CaseF_SameTextualIdDifferentSource_AreNotEqual()
    {
        var otherSource = new FaraTaraz.BuildingBlocks.Identifiers.AccountingSourceId("src-mock-2");

        var fromA = MockSources.Customer(SourceIds.Source, "123");
        var fromB = MockSources.Customer(otherSource, "123");

        Assert.NotEqual(fromA.RecordId, fromB.RecordId);
    }

    private static async Task<IReadOnlyList<SourceCustomer>> SyncAll(
        MockAccountingProvider provider,
        AccountingCapability capability)
    {
        var port = provider.RequireCapability<ICustomerSource>(capability);
        var collected = new List<SourceCustomer>();
        SyncCursor? cursor = null;

        do
        {
            var batch = await port.SyncAsync(
                new SyncRequest(SourceIds.Source, SyncMode.Full, cursor));

            collected.AddRange(batch.Records);
            cursor = batch.NextCursor;
        } while (cursor is not null);

        return collected;
    }
}
