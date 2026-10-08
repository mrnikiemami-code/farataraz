namespace FaraTaraz.Modules.AccountingSources;


/// <summary>
/// Marker interface for a single capability port.
///
/// Capability ports are independent and explicit. A provider implements only the
/// ports it genuinely supports. Ports are platform abstractions: they are NOT
/// provider-specific, and they never expose provider DTOs or field names.
///
/// Capability-specific metadata (<c>CapabilityName</c>, <c>SyncModeSupport</c>) lives on
/// <see cref="ISyncablePort{TRecord}"/> rather than here, so two capabilities can declare
/// independent values without a shared-member conflict.
/// </summary>
public interface ICapabilityPort
{
}

