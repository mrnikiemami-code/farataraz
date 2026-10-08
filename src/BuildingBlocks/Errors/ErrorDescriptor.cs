namespace FaraTaraz.BuildingBlocks.Errors;

/// <summary>
/// Immutable, provider-independent metadata describing one error contract.
///
/// This is a data contract, not a hierarchy: it carries a stable <see cref="Code"/>, a
/// <see cref="Category"/>, a <see cref="Severity"/>, whether the operation may be safely
/// retried, and a <see cref="SafeMessageKey"/>. It is intentionally not tied to any logging,
/// transport, or presentation concern so it can be reused for diagnostics, API responses,
/// and localization lookups alike.
/// </summary>
public sealed record ErrorDescriptor
{
    /// <summary>Stable machine-readable code for this error.</summary>
    public ErrorCode Code { get; }

    /// <summary>Broad bucket describing the nature of the error.</summary>
    public ErrorCategory Category { get; }

    /// <summary>How much attention this error warrants.</summary>
    public ErrorSeverity Severity { get; }

    /// <summary>Whether the operation may be safely retried.</summary>
    public bool IsRetryable { get; }

    /// <summary>
    /// Stable localization key for the human-facing message (for example
    /// <c>sync.source_record.unreadable</c>). Never a localized message itself.
    /// </summary>
    public string SafeMessageKey { get; }

    public ErrorDescriptor(
        ErrorCode code,
        ErrorCategory category,
        ErrorSeverity severity,
        bool isRetryable,
        string safeMessageKey)
    {
        Code = code;
        Category = category;
        Severity = severity;
        IsRetryable = isRetryable;
        SafeMessageKey = ValidateMessageKey(safeMessageKey);
    }

    private static string ValidateMessageKey(string safeMessageKey)
    {
        if (string.IsNullOrWhiteSpace(safeMessageKey))
        {
            throw new ArgumentException(
                "A safe message key must be a non-empty localization key.",
                nameof(safeMessageKey));
        }

        return safeMessageKey;
    }
}
