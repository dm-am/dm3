using System;
using System.Collections.Generic;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Rooms;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// DTO model for game post
/// </summary>
public class Post
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Parent room
    /// </summary>
    public Room Room { get; set; } = null!;

    /// <summary>
    /// Post character
    /// </summary>
    public Character Character { get; set; } = null!;

    /// <summary>
    /// Post author (lightweight reference)
    /// </summary>
    public UserRef Author { get; set; } = null!;

    /// <summary>
    /// Author's game role: DungeonMaster, Assistant, or null for player posts
    /// </summary>
    public string? AuthorGameRole { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Edit history (most recent first)
    /// </summary>
    public IEnumerable<PostEditInfo>? Edits { get; set; }

    /// <summary>
    /// Game text (in-character content)
    /// </summary>
    public PostBbText GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public CommonBbText MetagameText { get; set; } = null!;

    /// <summary>
    /// Dice roll results
    /// </summary>
    public IEnumerable<DiceRoll> DiceRolls { get; set; } = [];

    /// <summary>
    /// Sum of review scores
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Number of reviews
    /// </summary>
    public int ReviewCount { get; set; }

    /// <summary>
    /// 1-based place of the post in its room, in the order the room is written in.
    /// The same whatever filters and order the list was read with; null on the
    /// rated feed (GET /v1/posts), which does not number posts
    /// </summary>
    public int? Number { get; set; }

    /// <summary>
    /// Files attached to the post, oldest first
    /// </summary>
    public IEnumerable<PostAttachment> Attachments { get; set; } = [];

    /// <summary>
    /// The game the post was written in. Its rules span the game: a review of
    /// one post closes the reviews of the others there for three days, and a
    /// page holding posts of several games keeps that apart by this
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// What the viewer may do with this post, answered by the same code that
    /// refuses the request
    /// </summary>
    public PostViewer Viewer { get; set; } = new();
}

/// <summary>
/// What the viewer may do with a post. Each flag is asked by the same code the
/// write it stands for is refused by, so a page that offers by it offers only
/// what the server takes; the server refuses all the same.
/// </summary>
public class PostViewer
{
    /// <summary>
    /// Whether the viewer may review the post: not his own, not reviewed by him
    /// already, and no review of his in the same game during the last three
    /// days. False for a guest. The probation is not in it: a newbie is offered
    /// the neutral review
    /// </summary>
    public bool CanReview { get; set; }
}

/// <summary>
/// DTO model for a file attached to a post
/// </summary>
/// <remarks>
/// Deliberately without the key of the object in the bucket and without any
/// address the store would answer directly. The bytes come from
/// <see cref="Url"/>, an endpoint that decides the caller's right on every
/// request; a direct or signed address would work for whoever came to hold it,
/// for as long as it exists, which is not what "visible to this room" means.
/// </remarks>
public class PostAttachment
{
    /// <summary>
    /// Attachment identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// File name for display
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// MIME content type of the stored file
    /// </summary>
    public string ContentType { get; set; } = null!;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Intrinsic width in pixels, or null when unknown
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Intrinsic height in pixels, or null when unknown
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Address the file is served from (authorized on every request)
    /// </summary>
    public string Url { get; set; } = null!;

    /// <summary>
    /// Moment the file was attached
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// DTO model for post edit history entry
/// </summary>
public class PostEditInfo
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Edit timestamp (UTC)
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>
    /// Editor user (lightweight reference)
    /// </summary>
    public UserRef Editor { get; set; } = null!;
}
