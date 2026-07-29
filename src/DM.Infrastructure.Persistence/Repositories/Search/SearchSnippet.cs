using System.Text.RegularExpressions;

namespace DM.Infrastructure.Persistence.Repositories.Search;

/// <summary>
/// Preview text shared by every full-text search repository.
/// </summary>
/// <remarks>
/// Snippets are built from raw stored BBCode, which bypasses the viewer-scoped
/// Display render pipeline (BbConverter). Whatever that pipeline hides has to be
/// removed here instead, once, rather than in each repository — the two searches
/// would otherwise drift on what a preview may expose.
/// </remarks>
internal static class SearchSnippet
{
    /// <summary>
    /// Maximum preview length before an ellipsis is appended.
    /// </summary>
    public const int Length = 280;

    /// <summary>
    /// Matches a [private] block. Must stay identical to the pattern the
    /// Post.SearchVector generated column uses (DmDbContext), so that a preview
    /// can never show text the index refused to tokenize.
    /// </summary>
    public const string PrivateBlockPattern = @"\[private(=[^\]]*)?\][\s\S]*?\[/private\]";

    private static readonly Regex PrivateBlockRegex = new(
        PrivateBlockPattern,
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Removes [private] blocks from a raw snippet and collapses the whitespace
    /// the removal leaves behind. [private] is hidden from most readers by the
    /// viewer-scoped Display render, which search previews bypass. [mod] is
    /// public on read and is intentionally left intact.
    /// </summary>
    public static string StripPrivateBlocks(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var stripped = PrivateBlockRegex.Replace(text, " ");
        return Regex.Replace(stripped, @"\s+", " ");
    }

    /// <summary>
    /// Trims and cuts a preview to <see cref="Length"/>.
    /// </summary>
    public static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalized = text.Trim();
        return normalized.Length <= Length
            ? normalized
            : normalized[..Length] + "…";
    }
}
