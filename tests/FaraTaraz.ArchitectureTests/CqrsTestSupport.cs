namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Shared, deterministic test doubles for the W1-R2 CQRS + ownership proofs.
///
/// These are TEST-only helpers: they are NOT production implementations. The counting port
/// records provider invocations so tests can prove one-dispatch-per-page; the ownership fake
/// is a deterministic in-memory oracle so tests can prove fail-closed ownership enforcement.
/// Neither couples the tests to a concrete provider's internals beyond the neutral contract.
/// </summary>
public static class CqrsTestSupport
{
    /// <summary>
    /// Builds an <c>ISender</c> wired for the customer sync use case: the Application handlers
    /// (scanned from the assembly), a provider-independent capability port, and an ownership
    /// oracle. The provider adapter is the ONLY concrete wiring; the test observes only the
    /// neutral contract surface.
    /// </summary>
    public static ISender BuildSender(
        ISyncablePort<SourceCustomer> customers,
        IAccountingSourceOwnership ownership)
    {
        var services = new ServiceCollection();

        // Scan every Application handler in the Ingestion.Application assembly. MediatR 12.5.0
        // exposes only the Action<MediatRServiceConfiguration> surface.
        services.AddMediatR(
            config => config.RegisterServicesFromAssembly(
                typeof(SynchronizeCustomersHandler).Assembly));

        services.AddScoped<ISyncablePort<SourceCustomer>>(_ => customers);
        services.AddScoped<IAccountingSourceOwnership>(_ => ownership);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<ISender>();
    }

    /// <summary>
    /// A deterministic 5-customer Mock provider (page size 2, full + incremental).
    /// </summary>
    public static MockCapabilitySync<SourceCustomer> Customers5()
        => new(
            MockAccountingProvider.CustomersCapability,
            new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
                MockSources.Customer(SourceIds.Source, "C3"),
                MockSources.Customer(SourceIds.Source, "C4"),
                MockSources.Customer(SourceIds.Source, "C5"),
            },
            SyncModeSupport.FullAndIncremental);

    /// <summary>
    /// A customer capability that declares Full-only support, used to prove that an unsupported
    /// mode still fails explicitly through the handler (never silently accepted).
    /// </summary>
    public static MockCapabilitySync<SourceCustomer> CustomersFullOnly()
        => new(
            MockAccountingProvider.CustomersCapability,
            new[]
            {
                MockSources.Customer(SourceIds.Source, "C1"),
                MockSources.Customer(SourceIds.Source, "C2"),
            },
            SyncModeSupport.FullOnly);

    /// <summary>
    /// Wraps a capability port and counts each <c>SyncAsync</c> invocation, so tests can prove
    /// one-dispatch-per-page without observing the underlying provider's internals.
    /// </summary>
    public sealed class CountingCustomerPort : ISyncablePort<SourceCustomer>
    {
        private readonly MockCapabilitySync<SourceCustomer> _inner;
        private int _callCount;

        public CountingCustomerPort(MockCapabilitySync<SourceCustomer> inner) => _inner = inner;

        public int CallCount => Volatile.Read(ref _callCount);

        public string CapabilityName => _inner.CapabilityName;

        public SyncModeSupport SyncModeSupport => _inner.ModeSupport;

        public Task<SyncBatch<SourceCustomer>> SyncAsync(
            SyncRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return _inner.SyncAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Deterministic in-memory ownership oracle for tests. NOT a production implementation.
    ///
    /// Returns <c>true</c> only for an explicitly configured (tenant, source) ownership pair.
    /// Any other case — unknown tenant, unknown source, or <see cref="Fail"/> mode — returns
    /// <c>false</c> (fail-closed). A cancelled token propagates before any lookup.
    /// </summary>
    public sealed class InMemoryOwnershipFake : IAccountingSourceOwnership
    {
        private readonly HashSet<(TenantId, AccountingSourceId)> _owned;
        private readonly bool _fail;

        public InMemoryOwnershipFake(bool fail = false)
        {
            _owned = new HashSet<(TenantId, AccountingSourceId)>();
            _fail = fail;
        }

        /// <summary>Records that <paramref name="tenantId"/> owns <paramref name="sourceId"/>.</summary>
        public void Owns(TenantId tenantId, AccountingSourceId sourceId)
            => _owned.Add((tenantId, sourceId));

        public Task<bool> IsOwnedByAsync(
            TenantId tenantId,
            AccountingSourceId sourceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_fail)
            {
                // Lookup failure is fail-closed: NOT owned, never authorization success.
                return Task.FromResult(false);
            }

            return Task.FromResult(_owned.Contains((tenantId, sourceId)));
        }
    }

    /// <summary>Shared deterministic identifiers for the proofs.</summary>
    public static class SourceIds
    {
        public static readonly AccountingSourceId Source = new("src-mock-1");
    }
}
