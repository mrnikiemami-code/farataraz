namespace FaraTaraz.BuildingBlocks.Errors;

using System.Text.RegularExpressions;

/// <summary>
/// Stable, machine-readable identity of a kind of error.
///
/// The code is a platform-level token — for example <c>FT-ING-SYNC-003</c> — never a
/// localized message. It is comparable, hash-stable, and safe to carry across boundaries
/// (logs, API responses, diagnostics). The format is
/// <c>FT-DOMAIN-AREA-NNN</c>: a fixed platform prefix, a domain and area of uppercase
/// letters, and a fixed-width numeric sequence.
/// </summary>
public readonly record struct ErrorCode
{
    private static readonly Regex CodeFormat =
        new(@"^FT-[A-Z]+-[A-Z]+-\d{3}$", RegexOptions.Compiled);

    /// <summary>
    /// The validated code, for example <c>FT-ING-SYNC-003</c>.
    /// </summary>
    public string Value { get; }

    public ErrorCode(string value)
    {
        Value = Validate(value);
    }

    private static string Validate(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "An error code must be a non-empty string.", nameof(value));
        }

        if (!CodeFormat.IsMatch(value))
        {
            throw new ArgumentException(
                $"Error code '{value}' is malformed. Expected format " +
                "'FT-DOMAIN-AREA-NNN' (for example FT-ING-SYNC-003).", nameof(value));
        }

        return value;
    }

    public override string ToString() => Value;
}
