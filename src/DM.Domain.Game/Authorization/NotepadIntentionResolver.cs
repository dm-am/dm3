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
    /// <summary>
    /// Type of notepad (Master, Player or CharacterMaster)
    /// </summary>
    public NotepadType NotepadType { get; set; }

    /// <summary>
    /// Container identifier (GameId)
    /// </summary>
    public Guid ContainerId { get; set; }

    /// <summary>
    /// Owner identifier for the two character notepads (CharacterId)
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// Author of the entry, for the two intentions that are about one entry.
    /// Null for the notepad-wide questions - listing the entries, adding one -
    /// and a null here refuses Edit and Delete instead of matching whoever asks.
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
    /// Two questions, in this order. Access to the notepad decides who may be
    /// here at all and answers the same for every intention; the intention then
    /// narrows the two that are about an entry somebody already wrote. Reading
    /// and creating stay on access alone - a shared notepad whose entries only
    /// their authors could read would not be shared.
    ///
    /// Access first also means authorship opens nothing by itself: whoever left
    /// the game keeps no right over the notes they left behind.
    ///
    /// Three notepads hang off a game and the access half is where they part.
    /// The notepad of the game is the leads'; the notes of a player are that
    /// player's and shut to everyone else, the master included; the notes the
    /// leads keep about a character are the leads' and shut to the player who
    /// owns it. The two character notepads therefore never overlap - which is
    /// the point of writing them as separate types rather than as one notepad
    /// read differently by different people.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, NotepadIntention intention, NotepadAuthContext target)
    {
        var hasAccess = target.NotepadType switch
        {
            NotepadType.Master or NotepadType.CharacterMaster => IsAllowedForGameLeads(target),
            NotepadType.Player => IsAllowedForPlayerNotepad(user, target),
            _ => false
        };

        if (!hasAccess)
        {
            return false;
        }

        return intention switch
        {
            NotepadIntention.Read => true,
            NotepadIntention.Create => true,
            // An entry is its author's own words, so nobody rewrites it for
            // them: a lead who objects to one deletes it rather than edits it.
            NotepadIntention.Edit => IsEntryAuthor(user, target),
            // Delete adds the game master, who answers for what the notepads of
            // the game hold. The arm names the master and not every lead on
            // purpose: an assistant reaches the whole master notepad, and what
            // an assistant removes from it is what they wrote themselves. It
            // reaches no player's notes either way - the access half above has
            // already refused the master there.
            NotepadIntention.Delete => IsEntryAuthor(user, target) ||
                                       target.GameRoles.Contains(GameRole.Master)
        };
    }

    /// <summary>
    /// The notepad of the game and the notes kept about a character: master or
    /// assistant, and nobody else. The curating mentor is out of both - looking
    /// after a game lets a mentor speak in it, not read what its leads write
    /// down. So is site administration: only game roles are consulted here.
    /// </summary>
    private static bool IsAllowedForGameLeads(NotepadAuthContext target)
    {
        return target.GameRoles.HasEditAccess();
    }

    /// <summary>
    /// A player's own notes about their character, and the one notepad of a game
    /// its master cannot open. Leading the game is not a key to it: the player
    /// writes here for themselves, and a notepad the master could read would be
    /// a different thing than the one that was promised.
    /// </summary>
    private static bool IsAllowedForPlayerNotepad(IAuthorizationSubject user, NotepadAuthContext target)
    {
        return target.CharacterOwnerId.HasValue && target.CharacterOwnerId.Value == user.UserId;
    }

    /// <summary>
    /// Whether the asker wrote the entry the intention is about. An absent
    /// author is not a match: the context carries none for the notepad-wide
    /// questions, and neither of those two ever reaches here.
    /// </summary>
    private static bool IsEntryAuthor(IAuthorizationSubject user, NotepadAuthContext target) =>
        target.AuthorId.HasValue && target.AuthorId.Value == user.UserId;
}
