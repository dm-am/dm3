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
    /// Preview text. Always derived from the already-stripped/indexed text —
    /// [private] blocks are never present here.
    /// </summary>
    public string Snippet { get; set; } = "";
}
