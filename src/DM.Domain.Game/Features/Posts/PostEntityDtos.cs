using System;
using System.Collections.Generic;
using DM.Domain.Core.Content;

namespace DM.Domain.Game.Features.Posts;

public class CreatePostEntity
{
    public Guid PostId { get; set; }

    public Guid RoomId { get; set; }

    public Guid AuthorId { get; set; }

    public Guid? CharacterId { get; set; }

    public string GameText { get; set; } = null!;

    public string? MetagameText { get; set; }

    /// <summary>
    /// Resolved at save time and frozen, see <see cref="PrivateAddresseeSnapshot"/>.
    /// </summary>
    public string PrivateAddresseeSnapshotJson { get; set; } = PrivateAddresseeSnapshot.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Written in the post's transaction: a roll cannot be produced a second time.
    /// </summary>
    public IReadOnlyList<Posts.DiceRoll> DiceRolls { get; set; } = [];
}

public class UpdatePostEntity
{
    public Guid PostId { get; set; }

    /// <summary>
    /// The line of the edit history the save writes when it changes the post:
    /// the post row keeps its author and no editor, so the history is what says
    /// who changed it and when ("Отредактировано" under the post).
    /// </summary>
    public Guid PostEditId { get; set; }

    public Guid EditorUserId { get; set; }

    public DateTimeOffset EditedUtc { get; set; }

    /// <summary>
    /// Written when <see cref="ShouldChangeCharacter"/> is set
    /// </summary>
    public Guid? CharacterId { get; set; }

    public bool ShouldChangeCharacter { get; set; }

    public string GameText { get; set; } = null!;

    public string? MetagameText { get; set; }

    /// <summary>
    /// Recomputed on every save, keeping what an earlier one resolved, see <see cref="PrivateAddresseeSnapshot"/>.
    /// </summary>
    public string PrivateAddresseeSnapshotJson { get; set; } = PrivateAddresseeSnapshot.Empty;
}
