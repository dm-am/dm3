using System.Text.RegularExpressions;
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
    // Matches an opening [mod] / [mod=attr] or a closing [/mod] tag,
    // case-insensitive. The single bounded quantifier [^\]]* keeps the
    // pattern linear (no catastrophic backtracking / ReDoS); its cost is
    // bound by the content length. [modify], [mods], etc. do not match
    // because "mod" must be followed by an optional =attr and a closing ].
    private static readonly Regex ModTagRegex = new(
        @"\[/?mod(=[^\]]*)?\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Removes every [mod] / [mod=...] opening tag and matching [/mod]
    /// closing tag while keeping the inner text (unwrap, not delete).
    /// Returns the input unchanged when it is null, empty, or contains no
    /// [mod] markers.
    /// </summary>
    public static string StripUnauthorizedModBlocks(string rawBbCode) =>
        string.IsNullOrEmpty(rawBbCode) ? rawBbCode : ModTagRegex.Replace(rawBbCode, string.Empty);

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
