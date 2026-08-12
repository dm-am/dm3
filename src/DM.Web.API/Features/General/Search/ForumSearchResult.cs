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
    /// Preview of the matched text.
    /// </summary>
    public string Snippet { get; set; } = "";
}
