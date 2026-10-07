namespace FaraTaraz.BuildingBlocks.Accounting;

/// <summary>
/// Identity of an accounting provider family (e.g. Asan / Sepidar / Holoo / Mahak / Mock).
/// This is a platform-level identity token, NOT a provider-specific DTO or field.
/// </summary>
public readonly record struct ProviderId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Explicit set of capabilities an accounting provider may expose.
///
/// Capabilities are independent. A provider supports an arbitrary subset.
/// This enum is a declaration of capability, never a data container.
/// </summary>
[Flags]
public enum AccountingCapability
{
    None      = 0,
    Customers = 1 << 0,
    Products  = 1 << 1,
    Sales     = 1 << 2,
    Inventory = 1 << 3,
    Purchases = 1 << 4,
    Payments  = 1 << 5
}

/// <summary>
/// Thrown when a capability is requested that the provider does not support.
/// Unsupported capability is an explicit failure, never an empty collection.
/// </summary>
public sealed class CapabilityNotSupportedException : Exception
{
    public AccountingCapability Capability { get; }

    public CapabilityNotSupportedException(AccountingCapability capability)
        : base($"Accounting provider does not support the '{capability}' capability.")
    {
        Capability = capability;
    }
}
