using System;
using System.Collections.Generic;
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
    /// A marker is a bracket, an optional slash, the name in any casing, then
    /// either the closing bracket or "=" or whitespace and anything up to it that
    /// is not a bracket: <c>[mod]</c>, <c>[MOD]</c>, <c>[mod="x"]</c>,
    /// <c>[/mod x]</c>. That is more than the parser reads as the tag, on purpose
    /// — this removes a superset, and the superset has to hold by a property
    /// rather than by a list of cases.
    ///
    /// The property: no marker is left in the result. A single pass does not
    /// have it, because removing a marker joins the text on either side, and the
    /// join can be a marker: <c>[[mod]mod]</c> loses its inner marker and leaves
    /// the outer one standing, which is how a user below Moderator stored a mod
    /// block. So the text is written into the result one character at a time,
    /// and every time a closing bracket completes a marker at the end of what has
    /// been written, the marker is taken back out. A marker in the result would
    /// have been completed by its own closing bracket, at which moment it stood at
    /// the end, so none survives. Each character is written once and taken back
    /// at most once, and the one check per closing bracket reads a prefix of
    /// fixed length, so the pass is linear.
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
    public static string SanitizeForAuthor(string rawBbCode, UserRole authorRole) =>
        authorRole >= UserRole.Moderator ? rawBbCode : StripUnauthorizedModBlocks(rawBbCode);
}
