using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Authorization;

/// <summary>
/// Context for game notepad authorization (Master, Player and CharacterMaster
/// notepads only)
/// </summary>
public class NotepadAuthContext
{
    public NotepadType NotepadType { get; set; }

    /// <summary>
    /// Author of the entry; null for listing and adding, and a null refuses Edit and Delete.
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Game roles of the asker
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
    /// <remarks>
    /// Access to the notepad first, then the intention narrows the two about an existing entry,
    /// so authorship alone opens nothing. The player notepad is shut to the leads and the
    /// character master notepad to the player, so the two never overlap.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, NotepadIntention intention, NotepadAuthContext target)
    {
        var hasAccess = target.NotepadType switch
        {
            // Game roles only: neither the curating mentor nor site administration reads what the leads write.
            NotepadType.Master or NotepadType.CharacterMaster => target.GameRoles.HasEditAccess(),
            // The player writes here for himself, so leading the game is no key to it.
            NotepadType.Player => target.CharacterOwnerId == user.UserId,
            _ => false
        };

        if (!hasAccess)
        {
            return false;
        }

        return intention switch
        {
            NotepadIntention.Read or NotepadIntention.Create => true,
            // An entry is its author's words: a lead who objects deletes it rather than edits it.
            NotepadIntention.Edit => IsEntryAuthor(user, target),
            // The master answers for what the notepads hold; an assistant removes only his own.
            NotepadIntention.Delete => IsEntryAuthor(user, target) ||
                                       target.GameRoles.Contains(GameRole.Master)
        };
    }

    private static bool IsEntryAuthor(IAuthorizationSubject user, NotepadAuthContext target) =>
        target.AuthorId == user.UserId;
}
