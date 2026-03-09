using System;
using System.Collections.Generic;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Authorization;

/// <summary>
/// Context for game notepad authorization (Master and Player notepads only)
/// </summary>
public class NotepadAuthContext
{
    /// <summary>
    /// Type of notepad (Master or Player)
    /// </summary>
    public NotepadType NotepadType { get; set; }

    /// <summary>
    /// Container identifier (GameId)
    /// </summary>
    public Guid ContainerId { get; set; }

    /// <summary>
    /// Owner identifier for Player notepad (CharacterId)
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// Author of the entry (for edit/delete checks)
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Game roles (for Master/Player notepads)
    /// </summary>
    public IEnumerable<GameRole> GameRoles { get; set; } = Array.Empty<GameRole>();

    /// <summary>
    /// User ID of character owner (for Player notepad)
    /// </summary>
    public Guid? CharacterOwnerId { get; set; }
}

/// <inheritdoc />
internal class NotepadIntentionResolver : IIntentionResolver<NotepadIntention, NotepadAuthContext>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, NotepadIntention intention, NotepadAuthContext target)
    {
        return target.NotepadType switch
        {
            NotepadType.Master => IsAllowedForMasterNotepad(target),
            NotepadType.Player => IsAllowedForPlayerNotepad(user, target),
            _ => false
        };
    }

    private static bool IsAllowedForMasterNotepad(NotepadAuthContext target)
    {
        // Master notepad: only master or assistant can access
        return target.GameRoles.HasEditAccess();
    }

    private static bool IsAllowedForPlayerNotepad(IAuthorizationSubject user, NotepadAuthContext target)
    {
        // Player notepad: character owner OR master/assistant can access
        if (target.GameRoles.HasEditAccess())
        {
            return true;
        }

        // Character owner can access their own notepad
        return target.CharacterOwnerId.HasValue && target.CharacterOwnerId.Value == user.UserId;
    }
}
