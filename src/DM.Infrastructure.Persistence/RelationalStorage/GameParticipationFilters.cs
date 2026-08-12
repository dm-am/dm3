using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Whether a user takes part in a game.
/// </summary>
/// <remarks>
/// Not the same question as visibility, which is why it does not live beside
/// <see cref="GameAccessibilityFilters" />: that one answers who may read the
/// game, this one answers whose game it is. It is a filter of its own rather
/// than a few lines inside the repository because the startup warmup compiles
/// the same query and used to carry a copy of the rule inside the HTTP host,
/// where a sixth role added to the list would have been added to one copy only.
/// </remarks>
public static class GameParticipationFilters
{
    /// <summary>
    /// User is the master, the mentor, an assistant, a player holding an active
    /// character, or a reader of the game.
    /// </summary>
    /// <param name="dbContext">
    /// Context the query runs on. Readership is a row of the subscriptions
    /// table, which the game has no navigation to, so the set is reached through
    /// the context; EF Core translates it the way it translates a navigation.
    /// </param>
    /// <param name="userId">User whose participation is asked about</param>
    public static Expression<Func<DbGame, bool>> Participating(DmDbContext dbContext, Guid userId) => game =>
        // Master
        game.MasterId == userId ||
        // Mentor
        game.MentorId == userId ||
        // Assistant
        game.Assistants.Any(a => a.UserId == userId) ||
        // Player (has active character)
        game.Characters.Any(c => !c.IsRemoved && c.Status == CharacterStatus.Active && c.AuthorId == userId) ||
        // Reader (subscriber)
        dbContext.Subscriptions.Any(s =>
            s.TargetType == SubscriptionTargetType.Game &&
            s.TargetId == game.GameId &&
            s.SubscriberId == userId);
}
