using System.Collections.Generic;
using DM.Domain.Core.Dto;
using System;

namespace DM.Domain.Forum.Features.Search;

/// <summary>
/// A single forum full-text search result row: a topic or a comment on one.
/// </summary>
public class ForumSearchHit
{
    /// <summary>
    /// "topic" or "comment"
    /// </summary>
    public string EntityType { get; set; } = "";

    /// <summary>
    /// Topic id or comment id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Equals <see cref="Id"/> for a topic hit
    /// </summary>
    public Guid TopicId { get; set; }

    public int TopicNumber { get; set; }

    /// <summary>
    /// Shown as the result heading for both kinds of hit
    /// </summary>
    public string TopicTitle { get; set; } = "";

    public Guid BoardId { get; set; }

    public string BoardTitle { get; set; } = "";

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Preview of the matched text, split into the runs a reader sees.
    /// </summary>
    /// <remarks>
    /// Segments rather than a marked-up string: a string carrying the database's
    /// marks would be a field a client had to render as markup, unescaped.
    /// </remarks>
    public IReadOnlyList<SnippetSegment> SnippetSegments { get; set; } = [];

    /// <summary>
    /// The projected visible text of the body, as the repository read it.
    /// </summary>
    /// <remarks>
    /// Internal to the search: the window is cut from it when the query has no
    /// words to build one around. It never reaches the contract.
    /// </remarks>
    public string Snippet { get; set; } = "";

    /// <summary>
    /// Relevance score from ts_rank: a topic title carries weight A and its body
    /// weight B. Ordering is the search's, so the HTTP result does not repeat it.
    /// </summary>
    public float Rank { get; set; }
}
