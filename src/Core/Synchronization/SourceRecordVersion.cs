namespace FaraTaraz.Core.Synchronization;

/// <summary>
/// Optional change signal for a source record.
///
/// Not every provider exposes a revision number. Any subset of these signals may be
/// absent; ingestion compares the ones that are present. A deterministic
/// <see cref="ContentFingerprint"/> computed by the adapter is always comparable even when
/// the provider supplies no revision or modification time.
///
/// IMPORTANT: <see cref="ProviderModifiedAtUtc"/> is the PROVIDER-reported modification
/// time. It is distinct from the platform retrieval time in
/// <see cref="SourceProvenance"/>. Do not conflate the two.
/// </summary>
public sealed record SourceRecordVersion
{
    /// <summary>Provider-supplied revision/version marker, if the provider exposes one.</summary>
    public string? ProviderRevision { get; init; }

    /// <summary>Provider-reported modification time, if the provider exposes one.</summary>
    public DateTime? ProviderModifiedAtUtc { get; init; }

    /// <summary>
    /// Deterministic content fingerprint (see <see cref="SourceContentFingerprint"/>), if
    /// computed. Present or absent independently of the other signals.
    /// </summary>
    public string? ContentFingerprint { get; init; }

    /// <summary>True when at least one change signal is present.</summary>
    public bool HasChangeSignal =>
        ProviderRevision is not null
        || ProviderModifiedAtUtc is not null
        || ContentFingerprint is not null;

    /// <summary>
    /// True when this version represents a DIFFERENT state than <paramref name="previous"/>
    /// for the same source identity. Records with no change signal are treated as unchanged.
    /// </summary>
    public bool IsDifferentFrom(SourceRecordVersion? previous)
    {
        if (previous is null)
        {
            return HasChangeSignal;
        }

        if (!HasChangeSignal && !previous.HasChangeSignal)
        {
            return false;
        }

        return !Equals(ProviderRevision, previous.ProviderRevision)
            || !Equals(ProviderModifiedAtUtc, previous.ProviderModifiedAtUtc)
            || !Equals(ContentFingerprint, previous.ContentFingerprint);
    }
}
