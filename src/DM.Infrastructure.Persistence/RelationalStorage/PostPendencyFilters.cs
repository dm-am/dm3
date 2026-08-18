using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Which post pendencies of a room are expectations somebody may still be shown.
/// </summary>
/// <remarks>
/// A pendency is a row of its own and outlives the two links that give it
/// meaning: the character it names can lose access to the room and the master
/// who wrote it can leave the game, and the row stays. The rule below drops
/// those, and it is the rule behind every star on the site — the room list draws
/// one from it, and so does the participation list, which reads pendencies
/// without going through rooms at all. One text serves both shapes rather than
/// two copies drifting apart: a game would then say a turn is awaited that the
/// room it points at does not show.
/// </remarks>
public static class PostPendencyFilters
{
    /// <summary>
    /// The pendencies of a room: created by somebody who leads or plays in the
    /// game, and awaited from somebody who does.
    /// </summary>
    public static readonly Expression<Func<DbRoom, IEnumerable<DbPostPendency>>> OfRoom =
        room => room.PostPendencies.Where(p =>
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
            ));

    /// <summary>
    /// The same rule asked of a single pendency, for the reads that start at the
    /// pendencies table instead of at a room.
    /// </summary>
    /// <remarks>
    /// Lifted out of <see cref="OfRoom" /> rather than written again: the
    /// selection above reaches everything through the pendency (<c>p.Room</c>,
    /// never the room parameter), so its predicate is already a standalone rule
    /// and taking it whole is what keeps the two answers identical.
    /// </remarks>
    public static readonly Expression<Func<DbPostPendency, bool>> Genuine = PredicateOf(OfRoom);

    private static Expression<Func<DbPostPendency, bool>> PredicateOf(
        Expression<Func<DbRoom, IEnumerable<DbPostPendency>>> selection)
    {
        // Enumerable.Where takes a delegate, so the compiler puts the lambda in
        // the tree unquoted; the Quote arm is there in case the selection is ever
        // rewritten over IQueryable.
        var predicate = ((MethodCallExpression)selection.Body).Arguments[1];
        if (predicate is UnaryExpression { NodeType: ExpressionType.Quote } quoted)
        {
            predicate = quoted.Operand;
        }

        return (Expression<Func<DbPostPendency, bool>>)predicate;
    }
}
