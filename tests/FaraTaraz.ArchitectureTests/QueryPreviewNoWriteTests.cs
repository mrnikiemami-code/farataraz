namespace FaraTaraz.ArchitectureTests;

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using FaraTaraz.Modules.Ingestion.Contracts.Synchronization;
using Xunit;

/// <summary>
/// Wave 3 proof (task <c>FT-REBASELINE-W3-APPLICATION-GUARD-CLEANUP-001</c>) that the
/// <see cref="SynchronizeCustomersQuery"/> preview is genuinely read-only.
///
/// Application authority keeps exactly ONE durable customer-sync write path
/// (<c>RunCustomerSyncCommand</c>, which flows through <c>IIngestionWriteStore</c>). The query is
/// a legitimate preview: it drives exactly one bounded provider page and returns it, with no
/// persistence port, no unit of work and no DbContext reachable from it.
///
/// Two independent proofs:
///   1. structural — the handler's constructor parameters and fields contain no write/persistence
///      port type at all;
///   2. behavioral — the preview dispatches successfully in a container that registers NO write
///      store, NO checkpoint store and NO DbContext, so it cannot persist by construction.
///
/// The complementary "no direct persistence-write call" invariant is already enforced for every
/// production file outside the Infrastructure boundary by
/// <see cref="OwnershipPathAuthorizationGuard"/> (rule 1).
/// </summary>
public class QueryPreviewNoWriteTests
{
    private static readonly TenantId TenantId = new("tenant-preview-1");

    private static readonly string[] ForbiddenPersistenceTokens =
    {
        "IIngestionWriteStore",
        "IIngestionWriteSession",
        "IIngestionCheckpointStore",
        "IIngestionTrustedTenantScope",
        "IAccountingSourceProviderResolver",
        "DbContext",
        "Repository",
        "UnitOfWork",
    };

    [Fact]
    public void Query_preview_handler_depends_on_no_write_or_persistence_port()
    {
        var handler = typeof(SynchronizeCustomersHandler);

        var dependencyTypes = handler
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Concat(handler
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(field => field.FieldType))
            .ToList();

        Assert.NotEmpty(dependencyTypes);

        foreach (var type in dependencyTypes)
        {
            var fullName = type.FullName ?? type.Name;

            foreach (var token in ForbiddenPersistenceTokens)
            {
                Assert.False(
                    fullName.Contains(token, StringComparison.Ordinal),
                    $"The read-only preview handler must not depend on persistence type '{fullName}' " +
                    $"(matched '{token}'). Only the command is the durable write path.");
            }
        }
    }

    [Fact]
    public async Task Query_preview_dispatches_with_zero_persistence_registrations()
    {
        // CqrsTestSupport.BuildSender registers ONLY the capability port, the ownership oracle, the
        // active-source activation gate and MediatR. There is no IIngestionWriteStore, no
        // IIngestionCheckpointStore and no DbContext in the container, so a successful dispatch
        // proves the preview performs no persistence.
        var port = new CqrsTestSupport.CountingCustomerPort(CqrsTestSupport.Customers5());
        var ownership = new CqrsTestSupport.InMemoryOwnershipFake();
        ownership.Owns(TenantId, CqrsTestSupport.SourceIds.Source);

        var sender = CqrsTestSupport.BuildSender(port, ownership);

        var page = await sender.Send(new SynchronizeCustomersQuery(
            TenantContext.FromAuthenticatedPrincipal(TenantId),
            new SyncRequest(CqrsTestSupport.SourceIds.Source, SyncMode.Full)));

        Assert.Equal(1, port.CallCount);
        Assert.Equal(2, page.Records.Count);
        Assert.False(page.IsComplete);
    }
}
