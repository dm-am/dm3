using DM.Domain.Core.Dto;
using System.Collections.Generic;
using System;

namespace DM.Web.API.Features.General.Search;

/// <summary>
/// A single forum search result row.
/// </summary>
public class ForumSearchResult
{
    /// <summary>
    /// What was matched: "topic" or "comment".
    /// </summary>
    public string EntityType { get; set; } = "";

    /// <summary>
    /// Row identifier: topic id or comment id, used to jump to context.
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
    /// Topic title, the heading of the result row.
    /// </summary>
    public string TopicTitle { get; set; } = "";

    /// <summary>
    /// Board the topic lives on.
    /// </summary>
    public Guid BoardId { get; set; }

    /// <summary>
    /// Board title, the context of the result row.
    /// </summary>
    public string BoardTitle { get; set; } = "";

    /// <summary>
    /// Creation moment (UTC).
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Preview of the matched text, split into the runs a reader sees. A run marked
    /// as a match is where the search found its words.
    /// </summary>
    /// <remarks>
    /// Segments rather than a marked-up string: the database marks the match, and a
    /// string carrying those marks would be the one field of this contract a client
    /// had to render as markup — from a document nobody escaped. [private] blocks
    /// are never present, so addressee-only content is not exposed here.
    /// </remarks>
    public IReadOnlyList<SnippetSegment> Snippet { get; set; } = [];
}
