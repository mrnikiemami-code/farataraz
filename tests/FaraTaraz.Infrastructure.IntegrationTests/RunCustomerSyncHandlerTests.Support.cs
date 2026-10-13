namespace FaraTaraz.Infrastructure.IntegrationTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.AccountingSources.Authorization;

/// <summary>
/// Deterministic test doubles for the customer-sync handler tests: a provider resolver, a
/// fail-closed ownership oracle and a source-activation oracle. TEST-ONLY — none of these is a
/// production implementation. Kept in a separate partial file so the main test file stays within
/// the source-size budget without weakening any assertion.
/// </summary>
public sealed partial class RunCustomerSyncHandlerTests
{
    private sealed class FakeProviderResolver : IAccountingSourceProviderResolver
    {
        private readonly ProviderId? _provider;

        private FakeProviderResolver(ProviderId? provider) => _provider = provider;

        public static FakeProviderResolver Resolving(string provider) => new(new ProviderId(provider));
        public static FakeProviderResolver Missing() => new(null);

        public Task<ProviderId?> ResolveProviderAsync(
            TenantId tenantId, AccountingSourceId sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_provider);
    }

    private sealed class FakeOwnership : IAccountingSourceOwnership
    {
        private readonly Func<Task<bool>> _impl;

        private FakeOwnership(Func<Task<bool>> impl) => _impl = impl;

        public static FakeOwnership OwnsAll() => new(() => Task.FromResult(true));
        public static FakeOwnership OwnsNone() => new(() => Task.FromResult(false));
        public static FakeOwnership Fails() => new(() => throw new InvalidOperationException("ownership lookup failed"));

        public Task<bool> IsOwnedByAsync(TenantId tenantId, AccountingSourceId sourceId, CancellationToken cancellationToken = default)
            => _impl();
    }

    private sealed class FakeActivation : IAccountingSourceActivation
    {
        private readonly Func<Task<bool>> _impl;

        private FakeActivation(Func<Task<bool>> impl) => _impl = impl;

        public static FakeActivation Active() => new(() => Task.FromResult(true));
        public static FakeActivation Disabled() => new(() => Task.FromResult(false));

        public Task<bool> IsActiveAsync(TenantId tenantId, AccountingSourceId sourceId, CancellationToken cancellationToken = default)
            => _impl();
    }
}
