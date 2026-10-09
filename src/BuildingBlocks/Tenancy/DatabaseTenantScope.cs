namespace FaraTaraz.BuildingBlocks.Tenancy;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Carries the <b>trusted</b> tenant for a single unit of work (foundation).
///
/// Tenant authority originates only from a trusted execution context
/// (Constitution A.4/A.5); this scope is that per-operation binding. It is a scoped carrier,
/// never a static/global tenant, and it is never populated from client/LLM/MCP input.
/// Infrastructure layers use it to scope tenant-owned writes/reads.
///
/// When no trusted context is present the scope is <see cref="None"/> and every tenant-scoped
/// operation must <b>fail-closed</b> (treat as not owned / not authorized).
/// </summary>
public sealed class DatabaseTenantScope
{
    /// <summary>No trusted tenant bound. Operations must fail-closed.</summary>
    public static readonly DatabaseTenantScope None = new(null);

    /// <summary>The trusted tenant id, or <c>null</c> when no trusted context is bound.</summary>
    public TenantId? TenantId { get; }

    /// <summary>True only when a trusted tenant is bound.</summary>
    public bool IsTrusted => TenantId is not null;

    /// <summary>
    /// Creates a trusted scope from a trusted execution context. Only a trusted context
    /// (<see cref="TenantContextOrigin.AuthenticatedPrincipal"/>; see
    /// <see cref="TenantContext.AssertTrusted"/>) can create a trusted scope — this keeps the
    /// trust boundary fail-closed: an untrusted context throws, so no scope (and no tenant
    /// authority) is ever created from client/LLM/MCP input (Constitution A.4/A.5; ADR-010
    /// decision 5).
    /// </summary>
    public static DatabaseTenantScope FromTrusted(TenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AssertTrusted();
        return new DatabaseTenantScope(context.TenantId);
    }

    /// <summary>
    /// Internal constructor used by <c>None</c> and <see cref="FromTrusted"/>. Never called
    /// directly by callers: <c>None</c> carries no tenant (fail-closed) and <c>FromTrusted</c>
    /// only reaches here after a trusted context has been validated.
    /// </summary>
    private DatabaseTenantScope(TenantId? tenantId) => TenantId = tenantId;
}
