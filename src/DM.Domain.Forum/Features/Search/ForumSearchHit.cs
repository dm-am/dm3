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
    /// What was matched: "topic" or "comment".
    /// </summary>
    public string EntityType { get; set; } = "";

    /// <summary>
    /// Row identifier: topic id or comment id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Topic the row belongs to. Equals <see cref="Id"/> for a topic hit.
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Human-readable topic number the client builds the link from.
    /// </summary>
    public int TopicNumber { get; set; }

    /// <summary>
    /// Topic title, shown as the result heading for both kinds of hit.
    /// </summary>
    public string TopicTitle { get; set; } = "";

    /// <summary>
    /// Board the topic lives on.
    /// </summary>
    public Guid BoardId { get; set; }

    /// <summary>
    /// Board title, shown as the result context.
    /// </summary>
    public string BoardTitle { get; set; } = "";

    /// <summary>
    /// Creation moment (UTC).
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Preview of the matched text, split into the runs a reader sees.
    /// </summary>
    /// <remarks>
    /// Segments rather than a marked-up string: the database marks the match, and
    /// a string carrying those marks would be the one field of this contract a
    /// client had to render as markup — from a document nobody escaped.
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
    /// Relevance score from ts_rank, the sort key of the result set. A topic
    /// title carries weight A and its body weight B, so a title match outranks
    /// a body match on the same term.
    /// </summary>
    /// <remarks>
    /// Ordering is a property of the search, not of a row, so this stays inside
    /// the domain: the HTTP result presents rows already in order and does not
    /// repeat the score.
    /// </remarks>
    public float Rank { get; set; }
}
