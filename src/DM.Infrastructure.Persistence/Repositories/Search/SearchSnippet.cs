using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;

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
    /// Matches a [private] block — the same string the Post.SearchVector column is
    /// generated from, so that a preview can never show text the index refused to
    /// tokenize. An alias, not a second spelling.
    /// </summary>
    public const string PrivateBlockPattern = PrivateBlockMarkup.BlockPattern;

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
    /// <remarks>
    /// The fallback for a search with no words in it - a filter by author or by
    /// date matches every row equally, so there is no match to build a window
    /// around and the beginning of the text is the honest preview.
    /// </remarks>
    public static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalized = text.Trim();
        return normalized.Length <= Length
            ? normalized
            : normalized[..Length] + "…";
    }

    /// <summary>
    /// What the database is asked to mark a match with.
    /// </summary>
    /// <remarks>
    /// Control characters, because the alternative is a pair of strings that could
    /// occur in a body: any visible sentinel is one an author can type, and a
    /// preview would then split where nobody searched. The projection strips
    /// control characters out of every body for this reason, so a body can carry
    /// neither of these.
    /// </remarks>
    private const char MatchStart = '\u0011';

    private const char MatchEnd = '\u0012';

    /// <summary>
    /// Options handed to ts_headline: a window of about twenty words around the
    /// match, at most two fragments of it, marked with the sentinels above.
    /// </summary>
    public const string HeadlineOptions =
        "StartSel=\u0011, StopSel=\u0012, MaxWords=20, MinWords=8, MaxFragments=2, FragmentDelimiter=…";

    /// <summary>
    /// Splits a marked headline into the runs a reader sees, matched and not.
    /// </summary>
    /// <remarks>
    /// The marks never reach anybody: they are removed here, and what leaves is
    /// text and a flag. A preview that carried them as markup would be the one
    /// field of the contract a client had to render as html, and it would arrive
    /// from a document the database wrote and nobody escaped.
    /// </remarks>
    public static IReadOnlyList<SnippetSegment> Highlight(string? headline)
    {
        if (string.IsNullOrEmpty(headline))
        {
            return [];
        }

        var segments = new List<SnippetSegment>();
        var matched = false;
        var run = new StringBuilder();

        foreach (var character in headline)
        {
            if (character != MatchStart && character != MatchEnd)
            {
                run.Append(character);
                continue;
            }

            if (run.Length > 0)
            {
                segments.Add(new SnippetSegment(run.ToString(), matched));
                run.Clear();
            }

            matched = character == MatchStart;
        }

        if (run.Length > 0)
        {
            segments.Add(new SnippetSegment(run.ToString(), matched));
        }

        return segments;
    }

    /// <summary>
    /// A preview with nothing marked in it, for the search that had nothing to
    /// look for.
    /// </summary>
    public static IReadOnlyList<SnippetSegment> Plain(string? text)
    {
        var preview = Truncate(StripPrivateBlocks(text));
        return preview.Length == 0 ? [] : [new SnippetSegment(preview, false)];
    }
}
