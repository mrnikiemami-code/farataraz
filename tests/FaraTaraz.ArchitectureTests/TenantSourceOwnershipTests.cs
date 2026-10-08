namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using Xunit;

/// <summary>
/// Tenant-source ownership enforcement proof (W1-R2).
///
/// Proves the Application use case enforces fail-closed source ownership BEFORE any provider
/// work, through the provider-independent <c>IAccountingSourceOwnership</c> oracle:
///   1. trusted Tenant A + source owned by A → allowed
///   2. trusted Tenant A + source owned by B → denied
///   3. untrusted Tenant A + source owned by A → denied
///   4. unknown source → denied
///   5. ownership lookup failure → denied (no provider invocation)
///   6. cancellation during ownership check → propagated (no provider invocation)
///   7. provider invocation count is zero for EVERY rejected case
///
/// Source identity alone never grants authorization; a production always-<c>true</c> fake is
/// forbidden by construction (this test uses a deterministic in-memory oracle, not production
/// code). W2/W3 must supply the trusted source-ownership resolution this oracle stubs.
/// </summary>
public class TenantSourceOwnershipTests
{
    private static readonly TenantId TenantA = new("tenant-A");
    private static readonly TenantId TenantB = new("tenant-B");
    private static readonly AccountingSourceId OwnedByA = new("src-A");
    private static readonly AccountingSourceId OwnedByB = new("src-B");
    private static readonly AccountingSourceId Unknown = new("src-unknown");

    private static SynchronizeCustomersQuery Query(TenantContext tenant)
        => new(tenant, new SyncRequest(OwnedByA, SyncMode.Full));

    /// <summary>
    /// Builds a sender whose capability port is a counting proxy and whose ownership oracle is
    /// the supplied fake, so we can assert both the outcome and the provider invocation count.
    /// </summary>
    private static (ISender sender, CqrsTestSupport.CountingCustomerPort port) Build(
        CqrsTestSupport.InMemoryOwnershipFake ownership)
    {
        // The counting port is configured for OwnedByA (the query's source).
        var port = new CqrsTestSupport.CountingCustomerPort(
            new MockCapabilitySync<SourceCustomer>(
                MockAccountingProvider.CustomersCapability,
                new[]
                {
                    MockSources.Customer(OwnedByA, "C1"),
                    MockSources.Customer(OwnedByA, "C2"),
                },
                SyncModeSupport.FullAndIncremental));

        var sender = CqrsTestSupport.BuildSender(port, ownership);
        return (sender, port);
    }

    [Fact]
    public async Task TrustedTenant_owns_source_allows_sync()
    {
        var ownership = new CqrsTestSupport.InMemoryOwnershipFake();
        ownership.Owns(TenantA, OwnedByA);

        var (sender, port) = Build(ownership);

        var batch = await sender.Send(Query(TenantContext.FromAuthenticatedPrincipal(TenantA)));

        Assert.Equal(2, batch.Records.Count);
        Assert.True(port.CallCount == 1, "Provider invoked exactly once for the allowed case.");
    }

    [Fact]
    public async Task TrustedTenant_A_source_owned_by_B_denied()
    {
        var ownership = new CqrsTestSupport.InMemoryOwnershipFake();
        ownership.Owns(TenantB, OwnedByA); // OwnedByA belongs to B, not A.

        var (sender, port) = Build(ownership);

        var ex = await Assert.ThrowsAsync<UnauthorizedSourceException>(
            () => sender.Send(Query(TenantContext.FromAuthenticatedPrincipal(TenantA))));

        Assert.Equal(OwnedByA, ex.SourceId);
        Assert.True(port.CallCount == 0, "No provider work for a denied case.");
    }

    [Fact]
    public async Task UntrustedTenant_source_owned_by_A_denied()
    {
        var ownership = new CqrsTestSupport.InMemoryOwnershipFake();
        ownership.Owns(TenantA, OwnedByA);

        var (sender, port) = Build(ownership);

        // The context originates from client input, so AssertTrusted() rejects it before any
        // ownership check — the provider is never consulted.
        var ex = await Assert.ThrowsAsync<UnauthorizedTenantException>(
            () => sender.Send(Query(new TenantContext(TenantA, TenantContextOrigin.ClientInput))));

        Assert.True(port.CallCount == 0, "No provider work for a denied case.");
    }

    [Fact]
    public async Task Unknown_source_denied()
    {
        // No ownership is configured for this (tenant, source) pair => fail-closed.
        var ownership = new CqrsTestSupport.InMemoryOwnershipFake();

        var (sender, port) = Build(ownership);

        var ex = await Assert.ThrowsAsync<UnauthorizedSourceException>(
            () => sender.Send(Query(TenantContext.FromAuthenticatedPrincipal(TenantA))));

        Assert.True(port.CallCount == 0, "No provider work for a denied case.");
    }

    [Fact]
    public async Task Ownership_lookup_failure_denied_and_no_provider_invocation()
    {
        // Fail-closed: a lookup failure returns NOT owned, never authorization success.
        var ownership = new CqrsTestSupport.InMemoryOwnershipFake(fail: true);

        var (sender, port) = Build(ownership);

        var ex = await Assert.ThrowsAsync<UnauthorizedSourceException>(
            () => sender.Send(Query(TenantContext.FromAuthenticatedPrincipal(TenantA))));

        Assert.True(port.CallCount == 0, "Lookup failure must not reach the provider.");
    }

    [Fact]
    public async Task Cancellation_during_ownership_check_is_propagated()
    {
        var ownership = new CqrsTestSupport.InMemoryOwnershipFake();
        ownership.Owns(TenantA, OwnedByA);

        var (sender, port) = Build(ownership);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(
            () => sender.Send(Query(TenantContext.FromAuthenticatedPrincipal(TenantA)), cts.Token));

        // Cancellation is a caller action, never a provider failure classification, and the
        // ownership oracle throws before the provider is consulted.
        Assert.IsNotType<SyncProviderException>(ex);
        Assert.True(port.CallCount == 0, "No provider work for a cancelled case.");
    }

    [Fact]
    public async Task Every_rejected_case_leaves_the_provider_uninvoked()
    {
        // Aggregates every rejection path and asserts zero provider invocations for each.
        var cases = new List<(string Name, TenantContext Tenant, CqrsTestSupport.InMemoryOwnershipFake Ownership)>
        {
            ("owned_by_other_tenant",
                TenantContext.FromAuthenticatedPrincipal(TenantA),
                Owns(TenantB, OwnedByA)),
            ("untrusted",
                new TenantContext(TenantA, TenantContextOrigin.ClientInput),
                Owns(TenantA, OwnedByA)),
            ("unknown_source",
                TenantContext.FromAuthenticatedPrincipal(TenantA),
                new CqrsTestSupport.InMemoryOwnershipFake()),
            ("lookup_failure",
                TenantContext.FromAuthenticatedPrincipal(TenantA),
                new CqrsTestSupport.InMemoryOwnershipFake(fail: true)),
        };

        foreach (var (name, tenant, ownership) in cases)
        {
            var (sender, port) = Build(ownership);

            var error = await Record.ExceptionAsync(() => sender.Send(Query(tenant)));

            Assert.True(
                error is UnauthorizedSourceException or UnauthorizedTenantException,
                $"Case '{name}' must be rejected.");
            Assert.True(port.CallCount == 0, $"Case '{name}' must not invoke the provider.");
        }
    }

    private static CqrsTestSupport.InMemoryOwnershipFake Owns(TenantId tenant, AccountingSourceId source)
    {
        var fake = new CqrsTestSupport.InMemoryOwnershipFake();
        fake.Owns(tenant, source);
        return fake;
    }
}
