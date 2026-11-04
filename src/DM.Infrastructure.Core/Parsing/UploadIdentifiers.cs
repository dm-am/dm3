using System.Text.RegularExpressions;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Where a text may name a file of the site, for a reader nobody knows.
/// </summary>
/// <remarks>
/// The endpoint behind the address of a file reads its identifier in either
/// spelling, so any identifier is taken for one, wherever it stands and whatever
/// precedes it (<see cref="PrivateBlockReading"/> reads the same two spellings for
/// one known file).
/// </remarks>
public static class UploadIdentifiers
{
    /// <summary>Thirty-two hexadecimal digits, with the dashes of the "D" spelling or none.</summary>
    private static readonly Regex Any = new(
        "[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Whether the text holds an identifier of any file.</summary>
    public static bool Mentioned(string text) => Any.IsMatch(text);

    /// <summary>Where the first identifier in the text starts, or -1.</summary>
    public static int FirstAt(string text) => Any.Match(text) is { Success: true } match ? match.Index : -1;
}
