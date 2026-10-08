namespace FaraTaraz.BuildingBlocks.Diagnostics;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Immutable, caller-supplied diagnostic context for a single unit of work.
///
/// It carries the fields needed to make a log line structured and searchable —
/// <see cref="CorrelationId"/>, <see cref="TenantId"/>, <see cref="AccountingSourceId"/>,
/// <see cref="Module"/>, and <see cref="Operation"/> — without binding the platform to any
/// particular logging provider.
///
/// The context is intentionally not tied to <c>Microsoft.Extensions.Logging</c>: it is plain
/// data that a logging extension reads when it emits an event.
///
/// The caller must supply this context explicitly. Nothing in the platform generates a
/// <see cref="CorrelationId"/>, and there is no ambient/static/<c>AsyncLocal</c> context —
/// every caller that wants diagnostics constructs and passes one.
/// </summary>
public sealed class DiagnosticContext
{
    /// <summary>
    /// Creates a context. <see cref="CorrelationId"/>, <see cref="Module"/>, and
    /// <see cref="Operation"/> are required and must be nonblank; the tenant and source
    /// identifiers are optional.
    /// </summary>
    public DiagnosticContext(
        string correlationId,
        string module,
        string operation,
        TenantId? tenantId = null,
        AccountingSourceId? accountingSourceId = null)
    {
        CorrelationId = RequireNonBlank(correlationId, nameof(correlationId));
        Module = RequireNonBlank(module, nameof(module));
        Operation = RequireNonBlank(operation, nameof(operation));
        TenantId = tenantId;
        AccountingSourceId = accountingSourceId;
    }

    /// <summary>
    /// Caller-supplied correlation id for the unit of work. Required, nonblank.
    /// Never generated inside this type.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Owner tenant, when known. Optional; use <see cref="HasTenant"/> to test for presence.
    /// </summary>
    public TenantId? TenantId { get; }

    /// <summary>
    /// Accounting source, when known. Optional; use <see cref="HasSource"/> to test for presence.
    /// </summary>
    public AccountingSourceId? AccountingSourceId { get; }

    /// <summary>Platform module this work belongs to. Required, nonblank.</summary>
    public string Module { get; }

    /// <summary>Operation within the module. Required, nonblank.</summary>
    public string Operation { get; }

    /// <summary>True when a <see cref="TenantId"/> was supplied.</summary>
    public bool HasTenant => TenantId.HasValue;

    /// <summary>True when an <see cref="AccountingSourceId"/> was supplied.</summary>
    public bool HasSource => AccountingSourceId.HasValue;

    private static string RequireNonBlank(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"'{argumentName}' must be a nonblank string.", argumentName);
        }

        return value;
    }
}
