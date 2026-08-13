using DM.Domain.Core.Dto;
using System.Collections.Generic;
using System;

namespace DM.Web.API.Features.General.Search;

/// <summary>
/// A single unified message/post search result row.
/// </summary>
public class MessageSearchResult
{
    /// <summary>
    /// Source kind: "global", "chat" or "game".
    /// </summary>
    public string SourceType { get; set; } = "";

    /// <summary>
    /// Container identifier: chat id (global/chat) or game id (game).
    /// The frontend links the row via existing chat/game routes.
    /// </summary>
    public Guid SourceId { get; set; }

    /// <summary>
    /// Container label (chat/game title). Empty for direct chats — the client
    /// derives the counterpart participant name.
    /// </summary>
    public string? SourceTitle { get; set; }

    /// <summary>
    /// Row identifier: message id or post id (used for jump-to-context).
    /// </summary>
    public Guid Id { get; set; }

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
