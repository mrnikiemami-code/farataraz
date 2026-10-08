namespace FaraTaraz.BuildingBlocks.Errors;

/// <summary>
/// How much attention an error warrants. Provider-independent and ordered by severity.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Notices that do not block the operation.</summary>
    Warning = 0,

    /// <summary>The operation failed and requires attention.</summary>
    Error = 1,

    /// <summary>A severe failure that warrants immediate attention.</summary>
    Critical = 2,
}
