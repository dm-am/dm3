using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Game.Posts;

/// <summary>
/// DAL model for game room
/// </summary>
[Table("Rooms")]
public class Room : ISoftDeletable
{
    /// <summary>
    /// Room identifier
    /// </summary>
    [Key]
    public Guid RoomId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Sequential room number within the game (for URL, stable)
    /// </summary>
    public int RoomNumber { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Room access type
    /// </summary>
    public RoomAccessType AccessType { get; set; }

    /// <summary>
    /// Room type (default/chat)
    /// </summary>
    public RoomType Type { get; set; }

    /// <summary>
    /// Display order number
    /// </summary>
    public double OrderNumber { get; set; }

    /// <summary>
    /// Any user can read private messages within posts
    /// </summary>
    public bool ViewPrivateText { get; set; }

    /// <summary>
    /// Any user can see dice roll results
    /// </summary>
    public bool ViewDiceResults { get; set; }

    /// <summary>
    /// Dice rolling is enabled in this room
    /// </summary>
    public bool DiceEnabled { get; set; }

    /// <summary>
    /// Previous room identifier (for 2-linked-list)
    /// </summary>
    public Guid? PreviousRoomId { get; set; }

    /// <summary>
    /// Next room identifier (for 2-linked-list)
    /// </summary>
    public Guid? NextRoomId { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Linked chat identifier (for RoomType.Chat rooms)
    /// </summary>
    public Guid? ChatId { get; set; }

    /// <summary>
    /// Game
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    /// <summary>
    /// User who deleted the room
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Previous room (for 2-linked-list)
    /// </summary>
    [ForeignKey(nameof(PreviousRoomId))]
    public virtual Room? PreviousRoom { get; set; }

    /// <summary>
    /// Next room (for 2-linked-list)
    /// </summary>
    [ForeignKey(nameof(NextRoomId))]
    public virtual Room? NextRoom { get; set; }

    /// <summary>
    /// Room access links (characters and readers)
    /// </summary>
    [InverseProperty(nameof(RoomAccess.Room))]
    public virtual ICollection<RoomAccess> RoomAccesses { get; set; } = [];

    /// <summary>
    /// Posts
    /// </summary>
    [InverseProperty(nameof(Post.Room))]
    public virtual ICollection<Post> Posts { get; set; } = [];

    /// <summary>
    /// Post pendencies in room
    /// </summary>
    [InverseProperty(nameof(PostPendency.Room))]
    public virtual ICollection<PostPendency> PostPendencies { get; set; } = [];
}
