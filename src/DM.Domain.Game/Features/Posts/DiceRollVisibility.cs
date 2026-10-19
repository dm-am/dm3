using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Which dice rolls of a post a reader is given
/// </summary>
/// <remarks>
/// A roll with any of its three "Показывать всем результаты бросков" boxes clear (its own, the room's,
/// the game's) is read by the leads and the post author only. Everybody else gets the post without it,
/// since a roll without dice would read as a total of the bonus alone.
/// </remarks>
internal static class DiceRollVisibility
{
    public static IEnumerable<DiceRoll> VisibleTo(this IEnumerable<DiceRoll> rolls, Post post, Guid readerId)
    {
        var readsEveryRoll = !AnonymousIdentity.Is(readerId) &&
            (post.AuthorUserId == readerId || post.GameLeadUserIds.Contains(readerId));

        return readsEveryRoll ? rolls : rolls.Where(IsShownToAll).ToList();
    }

    private static bool IsShownToAll(DiceRoll roll) =>
        !roll.IsHidden && !roll.GameHideDiceResult && roll.RoomViewDiceResults;
}
