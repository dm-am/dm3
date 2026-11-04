using System.Diagnostics.CodeAnalysis;
using System.Text;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Content;

/// <summary>
/// Write-time sanitizer for the [mod] visual block.
///
/// [mod] renders as a green "moderator" block on the Comment and
/// GlobalChatMessage surfaces, so authoring it is restricted to Moderator+.
/// Reading [mod] is public — everyone sees an authorized mod block. When an
/// author below Moderator submits [mod], the tag is silently unwrapped on
/// save (markers removed, inner text kept) rather than rejected: no error,
/// no data loss.
/// </summary>
public static class ModBlockSanitizer
{
    private const string TagName = "mod";

    /// <summary>
    /// Removes every marker of the tag, opening and closing, while keeping the
    /// inner text (unwrap, not delete).
    /// </summary>
    /// <remarks>
    /// A marker is a bracket, an optional slash, the name in any casing, then the
    /// closing bracket, "=" or whitespace: a superset of what the parser reads as
    /// the tag. No marker is left in the result, including one a removal joins
    /// (<c>[[mod]mod]</c>): a marker is taken back out the moment its closing bracket
    /// completes it at the end of the result, which keeps the pass linear.
    /// </remarks>
    private static string StripUnauthorizedModBlocks(string rawBbCode)
    {
        if (string.IsNullOrEmpty(rawBbCode)) return rawBbCode;

        // The markers are looked for in the text the parser reads, and that text
        // is what is kept when anything is taken out: a control character between
        // two letters of the name is no marker in the stored text and a marker on
        // the page, where the parser has already removed it (see ParserInput).
        var text = ParserInput.Of(rawBbCode);
        if (!text.Contains(TagName, StringComparison.OrdinalIgnoreCase)) return rawBbCode;

        var result = new StringBuilder(text.Length);

        // Opening brackets of the result that no closing bracket follows yet, most
        // recent last. Only the most recent can start a marker a closing bracket
        // completes, because a marker holds no bracket between its two ends; the
        // older ones are kept for the moment a marker after them is taken out.
        var open = new List<int>();

        foreach (var character in text)
        {
            if (character == '[')
            {
                open.Add(result.Length);
                result.Append(character);
                continue;
            }

            if (character != ']')
            {
                result.Append(character);
                continue;
            }

            if (open.Count == 0)
            {
                result.Append(character);
                continue;
            }

            var start = open[^1];
            open.RemoveAt(open.Count - 1);
            if (IsMarker(result, start))
            {
                result.Length = start;
                continue;
            }

            // Not a marker: the closing bracket stays, and no opening bracket
            // written before it can start a marker any more, because a marker
            // holds no closing bracket.
            result.Append(character);
            open.Clear();
        }

        return result.ToString();
    }

    /// <summary>
    /// Whether the result from <paramref name="start"/> to its end, plus the
    /// closing bracket about to be written, is a marker of the tag.
    /// </summary>
    private static bool IsMarker(StringBuilder result, int start)
    {
        var index = start + 1;
        if (index < result.Length && result[index] == '/') index++;

        if (index + TagName.Length > result.Length) return false;
        for (var offset = 0; offset < TagName.Length; offset++)
        {
            if (char.ToLowerInvariant(result[index + offset]) != TagName[offset]) return false;
        }

        index += TagName.Length;
        return index == result.Length || result[index] == '=' || char.IsWhiteSpace(result[index]);
    }

    /// <summary>
    /// Role-gated variant applied at raw-BBCode write paths: unwraps [mod]
    /// for authors below <see cref="UserRole.Moderator"/>, otherwise returns
    /// the text unchanged. Moderator / SeniorModerator / Admin / System may
    /// author [mod]. Game master / assistant are game roles, not site roles,
    /// so they do not qualify.
    /// </summary>
    [return: NotNullIfNotNull(nameof(rawBbCode))]
    public static string? SanitizeForAuthor(string? rawBbCode, UserRole authorRole) =>
        rawBbCode is null || authorRole >= UserRole.Moderator ? rawBbCode : StripUnauthorizedModBlocks(rawBbCode);
}
