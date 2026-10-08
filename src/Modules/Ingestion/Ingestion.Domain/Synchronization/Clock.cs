namespace FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Injectable system clock. Platform retrieval timestamps must come from this abstraction
/// rather than calling <c>DateTime.UtcNow</c> throughout business/core code, so behavior
/// stays deterministic and testable.
/// </summary>
public interface ISystemClock
{
    /// <summary>Platform-observed current UTC time.</summary>
    DateTime NowUtc();
}

/// <summary>Default clock backed by <c>DateTime.UtcNow</c>.</summary>
public sealed class SystemClock : ISystemClock
{
    public DateTime NowUtc() => DateTime.UtcNow;
}
