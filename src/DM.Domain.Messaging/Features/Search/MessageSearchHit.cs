using System.Collections.Generic;
using DM.Domain.Core.Dto;
using System;

namespace DM.Domain.Messaging.Features.Search;

/// <summary>
/// A single unified full-text search result row (global chat message,
/// private/group chat message, or game post).
/// </summary>
public class MessageSearchHit
{
    /// <summary>
    /// Source kind: "global", "chat" or "game".
    /// </summary>
    public string SourceType { get; set; } = "";

    /// <summary>
    /// Container identifier: chat id (global/chat) or game id (game).
    /// </summary>
    public Guid SourceId { get; set; }

    /// <summary>
    /// Chat title or game title; may be empty for a direct chat, which the client names
    /// by the other participant.
    /// </summary>
    public string? SourceTitle { get; set; }

    /// <summary>
    /// Row identifier: message id or post id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Primary sort key.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Preview of the matched text, split into the runs a reader sees: as a marked-up
    /// string it would be the one field a client renders as markup, unescaped.
    /// </summary>
    public IReadOnlyList<SnippetSegment> SnippetSegments { get; set; } = [];

    /// <summary>
    /// The visible text of the body as the repository read it, [private] blocks never in
    /// it. Internal to the search: the window is cut from it when the query has no words
    /// to build one around.
    /// </summary>
    public string Snippet { get; set; } = "";
}
