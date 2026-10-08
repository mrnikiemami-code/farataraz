namespace FaraTaraz.Modules.MasterData;

/// <summary>FaraTaraz-owned canonical identity; never derived from an external provider code.</summary>
public readonly record struct CanonicalId
{
    public string Value { get; }

    public CanonicalId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A canonical identity is required.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value ?? string.Empty;
}
