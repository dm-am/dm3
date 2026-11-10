using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using Microsoft.EntityFrameworkCore;

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
    public const int Length = 280;

    /// <summary>
    /// The [private] block pattern is the string the Post.SearchVector column is
    /// generated from, so that a preview can never show text the index refused to
    /// tokenize. The pattern spells the casing it forgives itself, so .NET reads it
    /// with no options for case.
    /// </summary>
    private static readonly Regex PrivateBlockRegex = new(
        PrivateBlockMarkup.BlockPattern,
        RegexOptions.Compiled);

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

    public const string HeadlineOptions =
        "StartSel=\u0011, StopSel=\u0012, MaxWords=20, MinWords=8, MaxFragments=2, FragmentDelimiter=…";

    internal sealed class Body
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <remarks>
    /// A pass over the page rather than the ranked query: ts_headline re-parses its document,
    /// so it runs only for the rows a reader sees. It reads the document and not the vector,
    /// so the hidden block is cut again before the window is opened.
    /// </remarks>
    public static async Task<Dictionary<Guid, string>> Headlines(
        string query, CancellationToken ct, params (Guid[] Ids, IQueryable<Body> Bodies)[] sources)
    {
        var headlines = new Dictionary<Guid, string>();

        // A filter with no words in it matches every row equally: nothing to build a window around.
        if (string.IsNullOrWhiteSpace(query))
        {
            return headlines;
        }

        foreach (var (ids, bodies) in sources.Where(source => source.Ids.Length > 0))
        {
            foreach (var row in await bodies
                .Select(b => new
                {
                    b.Id,
                    Headline = EF.Functions.WebSearchToTsQuery(SearchTextConfiguration.Name, query)
                        .GetResultHeadline(
                            SearchTextConfiguration.Name,
                            DmDbContext.RegexpReplace(
                                b.Text, PrivateBlockMarkup.BlockPattern, " ", PrivateBlockMarkup.BlockPatternFlags),
                            HeadlineOptions),
                })
                .ToArrayAsync(ct))
            {
                headlines[row.Id] = row.Headline;
            }
        }

        return headlines;
    }

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

    public static IReadOnlyList<SnippetSegment> Plain(string? text)
    {
        var preview = Truncate(StripPrivateBlocks(text));
        return preview.Length == 0 ? [] : [new SnippetSegment(preview, false)];
    }
}
