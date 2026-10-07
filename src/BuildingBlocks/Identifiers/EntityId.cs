namespace FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Identity of a Tenant — the purchasing organization that consumes FaraTaraz.
/// A Tenant is NOT an accounting system, an accounting branch, or a customer.
/// </summary>
public readonly record struct TenantId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Identity of an AccountingSource — one configured accounting-system instance
/// that belongs to exactly one <see cref="TenantId"/>.
/// </summary>
public readonly record struct AccountingSourceId(string Value)
{
    public override string ToString() => Value;
}
