using System;
using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Game.Dto;

namespace DM.Services.Game.Authorization;

/// <summary>
/// Context for notepad authorization
/// </summary>
public class NotepadAuthContext
{
    /// <summary>
    /// Type of notepad
    /// </summary>
    public NotepadType NotepadType { get; set; }

    /// <summary>
    /// Container identifier (GameId for Player/Master, BlogId for Blog, UserId for User)
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
    /// Game participation (for Master/Player notepads)
    /// </summary>
    public GameParticipation GameParticipation { get; set; }

    /// <summary>
    /// User ID of character owner (for Player notepad)
    /// </summary>
    public Guid? CharacterOwnerId { get; set; }
}

/// <inheritdoc />
internal class NotepadIntentionResolver : IIntentionResolver<NotepadIntention, NotepadAuthContext>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, NotepadIntention intention, NotepadAuthContext target)
    {
        return target.NotepadType switch
        {
            NotepadType.User => IsAllowedForUserNotepad(user, intention, target),
            NotepadType.Master => IsAllowedForMasterNotepad(user, intention, target),
            NotepadType.Player => IsAllowedForPlayerNotepad(user, intention, target),
            NotepadType.Blog => IsAllowedForBlogNotepad(user, intention, target),
            _ => false
        };
    }

    private static bool IsAllowedForUserNotepad(AuthenticatedUser user, NotepadIntention intention, NotepadAuthContext target)
    {
        // User notepad: only the owner can access
        return target.ContainerId == user.UserId;
    }

    private static bool IsAllowedForMasterNotepad(AuthenticatedUser user, NotepadIntention intention, NotepadAuthContext target)
    {
        // Master notepad: only game authority (master/assistant) can access
        return target.GameParticipation.HasFlag(GameParticipation.Authority);
    }

    private static bool IsAllowedForPlayerNotepad(AuthenticatedUser user, NotepadIntention intention, NotepadAuthContext target)
    {
        // Player notepad: character owner OR game authority can access
        if (target.GameParticipation.HasFlag(GameParticipation.Authority))
        {
            return true;
        }

        // Character owner can access their own notepad
        return target.CharacterOwnerId.HasValue && target.CharacterOwnerId.Value == user.UserId;
    }

    private static bool IsAllowedForBlogNotepad(AuthenticatedUser user, NotepadIntention intention, NotepadAuthContext target)
    {
        // Blog notepad: use game participation field for blog participation
        // Authority in this context means blog owner or assistant
        return target.GameParticipation.HasFlag(GameParticipation.Authority);
    }
}
