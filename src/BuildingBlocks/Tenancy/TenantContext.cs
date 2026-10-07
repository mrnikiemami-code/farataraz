namespace FaraTaraz.BuildingBlocks.Tenancy;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Where a tenant context originated. This is the trust boundary for tenant authority.
/// Only <see cref="AuthenticatedPrincipal"/> is authoritative.
/// </summary>
public enum TenantContextOrigin
{
    /// <summary>Unknown — must be treated as untrusted.</summary>
    Unknown = 0,

    /// <summary>
    /// Derived from client / LLM / MCP input. Never authoritative.
    /// Forbidden: client sends tenantId = arbitrary-value.
    /// </summary>
    ClientInput = 1,

    /// <summary>
    /// Established by the trusted authentication/execution boundary.
    /// The only origin the platform treats as authoritative.
    /// </summary>
    AuthenticatedPrincipal = 2
}

/// <summary>
/// The trusted execution context for a single unit of work.
///
/// Tenant authority must originate from a trusted execution context
/// (<see cref="TenantContextOrigin.AuthenticatedPrincipal"/>), never from arbitrary
/// client, LLM, or MCP input.
/// </summary>
public sealed record TenantContext(TenantId TenantId, TenantContextOrigin Origin)
{
    /// <summary>True only when established by a trusted execution context.</summary>
    public bool IsTrusted => Origin == TenantContextOrigin.AuthenticatedPrincipal;

    /// <summary>
    /// Establishes a trusted tenant context from the authentication boundary.
    /// This is the ONLY sanctioned way to obtain tenant authority.
    /// </summary>
    public static TenantContext FromAuthenticatedPrincipal(TenantId tenantId)
        => new(tenantId, TenantContextOrigin.AuthenticatedPrincipal);

    /// <summary>
    /// Throws if the context is not established by a trusted execution context.
    /// Application use cases, REST, and MCP must call this before any tenant-scoped work.
    /// </summary>
    public void AssertTrusted()
    {
        if (!IsTrusted)
        {
            throw new UnauthorizedTenantException(
                "Tenant context is not established by a trusted execution context.");
        }
    }
}

/// <summary>Thrown when tenant authority is attempted from an untrusted origin.</summary>
public sealed class UnauthorizedTenantException : Exception
{
    public UnauthorizedTenantException(string message) : base(message)
    {
    }
}
