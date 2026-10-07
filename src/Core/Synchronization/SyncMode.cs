namespace FaraTaraz.Core.Synchronization;

/// <summary>
/// Synchronization mode requested for a capability.
///
/// A provider is NOT assumed to support <see cref="Incremental"/>. Capability ports
/// declare their support explicitly via <see cref="SyncModeSupport"/>; requesting an
/// unsupported mode fails explicitly and never silently falls back to <see cref="Full"/>.
/// </summary>
public enum SyncMode
{
    Full = 0,

    Incremental = 1
}
