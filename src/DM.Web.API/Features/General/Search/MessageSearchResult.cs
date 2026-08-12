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
    /// Preview text with [private] blocks stripped, so addressee-only game-post
    /// content is never exposed in search results. [mod] is public on read and
    /// is left intact.
    /// </summary>
    public string Snippet { get; set; } = "";
}
