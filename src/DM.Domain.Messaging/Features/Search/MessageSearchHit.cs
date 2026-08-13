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
    /// Human-readable container label (chat title or game title). May be empty
    /// for direct chats — the client derives the counterpart participant name.
    /// </summary>
    public string? SourceTitle { get; set; }

    /// <summary>
    /// Row identifier: message id or post id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Creation moment (UTC), primary sort key.
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
    /// The projected visible text of the body, as the repository read it. Always
    /// derived from the already-stripped/indexed text — [private] blocks are never
    /// present here.
    /// </summary>
    /// <remarks>
    /// Internal to the search: the window is cut from it when the query has no
    /// words to build one around. It never reaches the contract.
    /// </remarks>
    public string Snippet { get; set; } = "";
}
