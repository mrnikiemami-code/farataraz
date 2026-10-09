namespace FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;

using System.Collections.Generic;
using MediatR;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Domain.SourceModel;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Application-level query that retrieves exactly ONE bounded synchronization page for one
/// accounting source, established under a trusted Tenant context.
///
/// This is the W1-R2 bounded-page CQRS proof: a thin Application use case that delegates to
/// the provider-independent synchronization capability
/// (<c>ISyncablePort&lt;SourceCustomer&gt;</c>) and is dispatched through <c>ISender</c>.
///
/// The query returns a single <see cref="SyncBatch{TRecord}"/> page — never a full
/// collection. The <see cref="SyncBatch{TRecord}.NextCursor"/> is the caller's handle to
/// request the following page; pagination is caller-controlled, not driven by the handler.
///
/// The <see cref="TenantContext"/> is supplied by the trusted delivery boundary (never from
/// the request's <c>SyncRequest</c>); the handler asserts it is trusted and that the Tenant
/// owns the requested source before any tenant-scoped work.
/// </summary>
public sealed record SynchronizeCustomersQuery(TenantContext Tenant, SyncRequest Request)
    : IRequest<SyncBatch<SourceCustomer>>;
