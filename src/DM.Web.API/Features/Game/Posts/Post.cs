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
    /// Files attached to the post, oldest first
    /// </summary>
    public IEnumerable<PostAttachment> Attachments { get; set; } = [];
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
