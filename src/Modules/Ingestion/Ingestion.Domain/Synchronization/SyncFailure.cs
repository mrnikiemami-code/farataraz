namespace FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Provider-independent failure taxonomy sufficient for a future retry policy (W3).
///
/// W1 does not implement retry loops; it only classifies failures so W3 can decide.
/// <see cref="Cancellation"/> is not a provider failure — caller cancellation surfaces as
/// <see cref="OperationCanceledException"/> and is never wrapped as a <see cref="SyncProviderException"/>.
/// </summary>
public enum SyncFailureCategory
{
    /// <summary>Potentially retryable: timeout, temporarily unavailable, network, rate limit.</summary>
    Transient = 0,

    /// <summary>Not fixed by immediate retry: invalid credentials/config, malformed response, unsupported mode/capability.</summary>
    Permanent = 1,

    /// <summary>Caller-requested cancellation. NOT a provider failure.</summary>
    Cancellation = 2
}

/// <summary>
/// Base type for provider synchronization failures. Cancellation is intentionally NOT a
/// <see cref="SyncProviderException"/> — it propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public class SyncProviderException : Exception
{
    public SyncFailureCategory Category { get; }

    public SyncProviderException(SyncFailureCategory category, string message) : base(message)
    {
        Category = category;
    }
}

/// <summary>
/// Transient, retryable failure: provider timeout, temporarily unavailable, network
/// failure, or rate limiting.
/// </summary>
public sealed class SyncTransientException : SyncProviderException
{
    public SyncTransientException(string message)
        : base(SyncFailureCategory.Transient, message)
    {
    }
}

/// <summary>
/// Permanent failure: invalid credentials, invalid source configuration, or a malformed
/// provider response that violates the synchronization contract.
/// </summary>
public sealed class SyncPermanentException : SyncProviderException
{
    public SyncPermanentException(string message)
        : base(SyncFailureCategory.Permanent, message)
    {
    }
}

/// <summary>
/// Requested synchronization mode is not supported by this capability. Permanent failure;
/// never silently fall back to another mode.
/// </summary>
public sealed class SyncModeNotSupportedException : SyncProviderException
{
    public SyncMode Mode { get; }

    public SyncModeNotSupportedException(SyncMode mode)
        : base(SyncFailureCategory.Permanent,
               $"Synchronization mode '{mode}' is not supported by this capability.")
    {
        Mode = mode;
    }
}

/// <summary>
/// A cursor is malformed (empty/whitespace token, null scope). Fails explicitly; it must
/// never silently restart a full synchronization.
/// </summary>
public sealed class InvalidSyncCursorException : SyncProviderException
{
    public InvalidSyncCursorException(string message)
        : base(SyncFailureCategory.Permanent, message)
    {
    }
}

/// <summary>
/// A resumed cursor does not belong to the requested source/capability. Fails explicitly;
/// a cursor from one source/capability must not be silently accepted for another.
/// </summary>
public sealed class SyncCursorScopeMismatchException : SyncProviderException
{
    public SyncCursorScopeMismatchException(string message)
        : base(SyncFailureCategory.Permanent, message)
    {
    }
}
