namespace FaraTaraz.BuildingBlocks.Errors;

/// <summary>
/// Broad, provider-independent bucket describing the nature of an error.
///
/// Values are stable machine identifiers; never treat them as display text.
/// </summary>
public enum ErrorCategory
{
    /// <summary>The request violated a rule (validation, invariants, or state).</summary>
    Validation = 0,

    /// <summary>The principal lacked authority for the operation.</summary>
    Authorization = 1,

    /// <summary>A expected resource was not present.</summary>
    NotFound = 2,

    /// <summary>The request conflicts with the current state (optimistic or semantic).</summary>
    Conflict = 3,

    /// <summary>A transient provider failure that may succeed on retry.</summary>
    ProviderTransient = 4,

    /// <summary>A permanent provider failure that will not resolve on retry.</summary>
    ProviderPermanent = 5,

    /// <summary>Platform infrastructure (transport, configuration, infrastructure) failed.</summary>
    Infrastructure = 6,

    /// <summary>An unexpected, unclassified failure.</summary>
    Unexpected = 7,
}
