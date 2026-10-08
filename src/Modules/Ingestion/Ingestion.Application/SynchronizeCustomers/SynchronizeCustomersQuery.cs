namespace FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers;

using System.Collections.Generic;
using MediatR;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Application-level query that runs a bounded, resumable customer synchronization for one
/// accounting source, established under a trusted Tenant context.
///
/// This is the W1-R1 CQRS proof: a thin Application use case that delegates to the
/// provider-independent synchronization capability (<c>ISyncablePort&lt;SourceCustomer&gt;</c>)
/// and is dispatched through <c>ISender</c>. The <see cref="TenantContext"/> is supplied by the
/// trusted delivery boundary (never from the request's <c>SyncRequest</c>); the handler asserts
/// it is trusted before any tenant-scoped work, encoding the tenant-authority invariant.
/// </summary>
public sealed record SynchronizeCustomersQuery(TenantContext Tenant, SyncRequest Request)
    : IRequest<IReadOnlyList<SourceCustomer>>;
