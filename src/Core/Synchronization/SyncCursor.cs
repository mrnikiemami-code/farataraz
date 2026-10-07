namespace FaraTaraz.Core.Synchronization;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Platform-level scope a cursor belongs to. This is NOT provider cursor semantics — it is
/// a lightweight envelope that lets orchestration verify a cursor belongs to the requested
/// source/capability WITHOUT understanding the opaque <c>Token</c>.
/// </summary>
public sealed record SyncCursorScope(AccountingSourceId SourceId, string Capability)
{
    public override string ToString() => $"src({SourceId})@{Capability}";
}

/// <summary>
/// Opaque, immutable synchronization boundary returned by a capability and consumed by the
/// caller to resume.
///
/// The <c>Token</c> is provider-internal (page token, sequence number, timestamp +
/// tie-breaker, or composite checkpoint) and must never be interpreted by the platform.
/// The <see cref="Scope"/> is platform-managed so a cursor from one source/capability is
/// rejected for another. Construction validates the token; resume validation is exposed by
/// <see cref="SyncCursors"/>.
/// </summary>
public sealed class SyncCursor
{
    public string Token { get; }

    public SyncCursorScope Scope { get; }

    public SyncCursor(string token, SyncCursorScope scope)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidSyncCursorException("Cursor token must be a non-empty string.");
        }

        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Token = token;
    }

    public override string ToString() => $"{Scope}!{Token}";
}

/// <summary>
/// Cursor validation helpers shared by adapters and orchestration.
/// </summary>
public static class SyncCursors
{
    /// <summary>
    /// Validates that <paramref name="request.Cursor"/> (if present) belongs to the
    /// requested source and capability. Throws explicitly on mismatch; it never silently
    /// restarts a full synchronization. Returns <c>true</c> when no cursor is supplied.
    /// </summary>
    public static bool ValidateResume(SyncRequest request, string capability)
    {
        if (request.Cursor is null)
        {
            return true;
        }

        if (request.Cursor.Scope.SourceId != request.SourceId)
        {
            throw new SyncCursorScopeMismatchException(
                $"Cursor belongs to source {request.Cursor.Scope.SourceId}, but request is for {request.SourceId}.");
        }

        if (!string.Equals(request.Cursor.Scope.Capability, capability, StringComparison.Ordinal))
        {
            throw new SyncCursorScopeMismatchException(
                $"Cursor belongs to capability '{request.Cursor.Scope.Capability}', but request is for '{capability}'.");
        }

        return true;
    }
}
