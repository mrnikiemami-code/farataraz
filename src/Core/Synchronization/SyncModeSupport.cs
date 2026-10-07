namespace FaraTaraz.Core.Synchronization;

/// <summary>
/// Declares which synchronization modes a capability genuinely supports.
///
/// Support is capability-specific, not provider-global: one capability may support
/// Incremental while another does not. This is a declaration, never an inference from
/// empty data (empty dataset ≠ unsupported capability).
/// </summary>
public readonly record struct SyncModeSupport(bool SupportsIncremental)
{
    /// <summary>Full synchronization only (no incremental).</summary>
    public static SyncModeSupport FullOnly => new(false);

    /// <summary>Full and incremental synchronization.</summary>
    public static SyncModeSupport FullAndIncremental => new(true);

    /// <summary>
    /// True when the capability supports the requested mode. Full is always supported for
    /// a declared capability; Incremental is supported only when <see cref="SupportsIncremental"/>.
    /// </summary>
    public bool Supports(SyncMode mode) => mode switch
    {
        SyncMode.Full => true,
        SyncMode.Incremental => SupportsIncremental,
        _ => false
    };
}
