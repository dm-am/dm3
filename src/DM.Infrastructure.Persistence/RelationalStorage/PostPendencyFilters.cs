using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DM.Infrastructure.Persistence.Shared.Queries;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Which post pendencies of a room are expectations somebody may still be shown.
/// </summary>
/// <remarks>
/// A pendency outlives the links that give it meaning: its character can lose the
/// room and its master can leave the game. The room list and the participation list
/// read the one rule, so a game never awaits a turn its room does not show.
/// </remarks>
public static class PostPendencyFilters
{
    /// <summary>
    /// A pendency created by somebody who leads or plays in the game, and awaited
    /// from somebody who does.
    /// </summary>
    public static readonly Expression<Func<DbPostPendency, bool>> Genuine = p =>
        p.WaitingForUserId != null &&
        (
            p.Room.Game.MasterId == p.CreatedById ||
            p.Room.Game.Assistants.Any(a => a.UserId == p.CreatedById) ||
            p.Room.RoomAccesses.Any(a => a.Character != null && a.Character.AuthorId == p.CreatedById)
        ) &&
        (
            p.Room.Game.MasterId == p.WaitingForUserId ||
            p.Room.Game.Assistants.Any(a => a.UserId == p.WaitingForUserId) ||
            p.Room.RoomAccesses.Any(a => a.Character != null && a.Character.AuthorId == p.WaitingForUserId)
        );

    /// <summary>
    /// The genuine pendencies of a room. Declared after <see cref="Genuine" />,
    /// which the expansion reads.
    /// </summary>
    public static readonly Expression<Func<DbRoom, IEnumerable<DbPostPendency>>> OfRoom =
        ExpressionSplicer.Expand<Func<DbRoom, IEnumerable<DbPostPendency>>>(
            room => room.PostPendencies.Where(p => Genuine.Splice(p)));
}
