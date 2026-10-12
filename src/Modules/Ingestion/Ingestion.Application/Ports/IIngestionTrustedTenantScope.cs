namespace FaraTaraz.Modules.Ingestion.Application.Ports;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Read-only port exposing the tenant id that the Infrastructure persistence adapter resolved from
/// the TRUSTED <c>DatabaseTenantScope</c> for the current unit of work (task M3/M4).
///
/// It exists so Application orchestration can prove that the trusted <c>TenantContext</c> it is
/// about to act upon is the SAME tenant the adapter will write under. A disagreement means the
/// adapter would persist under a different tenant than the one whose ownership was admitted — a
/// cross-tenant hazard — so the use case MUST compare and fail closed before any provider call or
/// write. This is a read-side agreement check and never makes a caller-supplied id an authority.
/// </summary>
public interface IIngestionTrustedTenantScope
{
    /// <summary>The tenant bound to the trusted execution context, or <c>null</c> when none is bound.</summary>
    TenantId? TrustedTenantId { get; }
}
