using System;
using System.Collections.Generic;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.Posts;

public class Post
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public GeneralUser Author { get; set; } = null!;

    public CharacterShort Character { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// In-character text
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Out-of-character commentary
    /// </summary>
    public string MetagameText { get; set; } = null!;

    /// <summary>
    /// Sum of review scores
    /// </summary>
    public int Rating { get; set; }

    public int ReviewCount { get; set; }

    /// <summary>
    /// 1-based place in the room by creation moment, then identifier; null in the rated feed
    /// </summary>
    public int? Number { get; set; }

    /// <summary>
    /// Null for a player
    /// </summary>
    public string? AuthorGameRole { get; set; }

    /// <summary>
    /// For the rated feed
    /// </summary>
    public RoomRef? Room { get; set; }

    public IEnumerable<Posts.DiceRoll> DiceRolls { get; set; } = [];

    /// <summary>
    /// Most recent first
    /// </summary>
    public IEnumerable<Posts.PostEdit> Edits { get; set; } = [];

    public Guid AuthorUserId { get; set; }

    public Guid GameId { get; set; }

    /// <summary>
    /// The game setting "Показывать всем скрытые блоки", opening the addressed [private] blocks
    /// and the marked files of every post of the game to every reader of its room.
    /// </summary>
    public bool GameShowPrivateMessages { get; set; }

    /// <summary>
    /// The room setting of the same words, for the posts of one room.
    /// </summary>
    public bool RoomViewPrivateText { get; set; }

    public Guid GameMasterUserId { get; set; }

    public IReadOnlyCollection<Guid> GameAssistantUserIds { get; set; } = [];

    /// <summary>
    /// Mentors are not leads.
    /// </summary>
    /// <remarks>
    /// Joined here because Npgsql cannot translate a Concat onto an in-memory array in the projection.
    /// The empty id is left out: it is the one a guest carries.
    /// </remarks>
    public IReadOnlyCollection<Guid> GameLeadUserIds =>
        AnonymousIdentity.WithoutAnonymous([GameMasterUserId, .. GameAssistantUserIds]);

    /// <summary>
    /// Owner ids of every [private] block in <see cref="GameText"/>, parsed by the API layer
    /// because ProjectTo cannot deserialize JSON.
    /// </summary>
    public string PrivateAddresseeSnapshotJson { get; set; } = PrivateAddresseeSnapshot.Empty;

    /// <summary>
    /// Oldest first; filled by a batched read after the page, not a subquery per post.
    /// </summary>
    public IReadOnlyCollection<PostAttachment> Attachments { get; set; } = [];

    /// <summary>
    /// The game text with what decides who reads its hidden blocks.
    /// </summary>
    /// <remarks>
    /// The one place those fields of the post are read together: the render context of the text and
    /// the rule of its attachments (PostAttachmentVisibility) are both built from this, so the two
    /// cannot read a block for different readers.
    /// </remarks>
    public PrivateBlockText GameTextPrivateBlocks() => new(
        GameText,
        AuthorUserId,
        GameLeadUserIds,
        PrivateAddresseeSnapshotJson,
        GameShowPrivateMessages,
        RoomViewPrivateText);
}
