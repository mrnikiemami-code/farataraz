namespace FaraTaraz.BuildingBlocks.Configuration;

/// <summary>
/// Thrown by a module composition root when no usable connection string is supplied.
///
/// This is a fail-closed configuration guard (FT-CONFIG-001): a missing / empty / whitespace
/// connection string is rejected with a deterministic exception <b>before</b> any
/// <c>DbContext</c> is registered, so a module never silently binds to an embedded default or an
/// implicit localhost / PostgreSQL fallback. The caller must supply an explicit, nonempty
/// connection string; a production deployment that omits configuration fails closed rather than
/// connecting to an undocumented local database.
/// </summary>
public sealed class ConnectionConfigurationException : Exception
{
    public ConnectionConfigurationException(string message) : base(message)
    {
    }
}
