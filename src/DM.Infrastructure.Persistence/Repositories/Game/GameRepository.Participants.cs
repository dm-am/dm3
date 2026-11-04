using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Subscriptions;
using DM.Infrastructure.Persistence.Shared.Users;
using GameDto = DM.Domain.Game.Features.Games.Game;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// The people of a game a read carries: its master, assistants and characters,
/// and whose turn it is
/// </summary>
internal partial class GameRepository
{
    /// <summary>
    /// Populate the aggregated fields of Game DTOs the projection cannot produce
    /// (characters, players, subscribers, review counts, pending invitations).
    /// Every read path that returns a Game or GameDetails calls it; all queries are
    /// batched, so the cost is per table, not per game.
    /// </summary>
    /// <param name="games">Games to hydrate</param>
    /// <param name="userId">
    /// The user the read is on behalf of. Only <see cref="GameDto.IsViewerSubscriber" />
    /// and the turn marker (<see cref="GameDto.AwaitsViewerTurn" /> with its
    /// character names) depend on it; every other field here is the same for all
    /// viewers. <see cref="Guid.Empty" /> for an anonymous read.
    /// </param>
    /// <param name="ct">Cancellation token</param>
    private async Task EnrichGamesAsync<T>(
        IReadOnlyList<T> games,
        Guid userId,
        CancellationToken ct) where T : GameDto
    {
        if (games.Count == 0) return;
        var gameIds = games.Select(g => g.Id).ToHashSet();

        // PcCount — active non-NPC characters per game, single GROUP BY.
        var pcCounts = await _dbContext.Characters
            .Where(c => gameIds.Contains(c.GameId) && c.Status == CharacterStatus.Active && !c.IsNpc)
            .GroupBy(c => c.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

        foreach (var game in games)
        {
            game.Recruitment.PcCount = pcCounts.GetValueOrDefault(game.Id, 0);
        }

        // Players: unique authors of active non-NPC characters, projected so no whole
        // user row (password hash included) is read.
        var playerData = await _dbContext.Characters
            .AsNoTracking()
            .Where(c => gameIds.Contains(c.GameId) && c.Status == CharacterStatus.Active && !c.IsNpc &&
                        c.Author != null)
            .Select(c => new
            {
                c.GameId,
                Author = new GeneralUser
                {
                    UserId = c.Author!.UserId,
                    Username = c.Author.Username,
                    Role = c.Author.Role,
                    Status = c.Author.Status,
                    LastActivityUtc = c.Author.LastActivityUtc,
                    RegisteredUtc = c.Author.CreatedUtc,
                    // Carried on purpose: IsNewbie derives from it, and dropping it
                    // would silently mark every player a newbie.
                    QuantityRating = c.Author.QuantityRating,
                    Picture = AvatarProjections.From(c.Author.AvatarUpload),
                },
            })
            .ToListAsync(ct);

        var playersMap = playerData
            .GroupBy(c => c.GameId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => c.Author).DistinctBy(u => u.UserId).ToList());

        // Subscriber summary — the total, whether this viewer is one of them, and
        // the capped preview of names, all from one statement.
        var subscriberSummaries = await SubscriberSummaries.ByTarget(
            _dbContext.Subscriptions, SubscriptionTargetType.Game, gameIds, userId, ct);

        foreach (var game in games)
        {
            game.Players = playersMap.TryGetValue(game.Id, out var players)
                ? players.ToList()
                : [];

            var summary = subscriberSummaries.GetValueOrDefault(game.Id);
            game.SubscribersCount = summary?.Count ?? 0;
            game.IsViewerSubscriber = summary?.ViewerSubscribed ?? false;
            game.SubscriberUsernames = summary?.Preview ?? [];
        }

        // Invitation tokens in one query, projected for the reason the players are.
        var tokens = await _dbContext.Tokens
            .AsNoTracking()
            .Where(t => !t.IsRemoved &&
                        t.EntityId.HasValue &&
                        gameIds.Contains(t.EntityId.Value) &&
                        (t.Type == TokenType.GameAssistantInvitation ||
                         t.Type == TokenType.GamePlayerInvitation ||
                         t.Type == TokenType.GameReaderInvitation))
            .Select(t => new
            {
                GameId = t.EntityId!.Value,
                t.Type,
                t.UserId,
                User = new GeneralUser
                {
                    UserId = t.User.UserId,
                    Username = t.User.Username,
                    Role = t.User.Role,
                    Status = t.User.Status,
                    LastActivityUtc = t.User.LastActivityUtc,
                    RegisteredUtc = t.User.CreatedUtc,
                    QuantityRating = t.User.QuantityRating,
                    Picture = AvatarProjections.From(t.User.AvatarUpload),
                },
            })
            .ToListAsync(ct);

        var tokensByGame = tokens.GroupBy(t => t.GameId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var game in games)
        {
            if (tokensByGame.TryGetValue(game.Id, out var gameTokens))
            {
                var assistantToken = gameTokens.FirstOrDefault(t => t.Type == TokenType.GameAssistantInvitation);
                game.PendingAssistant = assistantToken?.User;
                game.PendingInvitedUserIds = gameTokens
                    .Where(t => t.Type == TokenType.GamePlayerInvitation || t.Type == TokenType.GameReaderInvitation)
                    .Select(t => t.UserId)
                    .ToHashSet();
                game.PendingPlayerInvitedUserIds = gameTokens
                    .Where(t => t.Type == TokenType.GamePlayerInvitation)
                    .Select(t => t.UserId)
                    .ToHashSet();
            }
            else
            {
                game.PendingAssistant = null;
                game.PendingInvitedUserIds = new HashSet<Guid>();
                game.PendingPlayerInvitedUserIds = new HashSet<Guid>();
            }
        }

        // Game / post review counts — both batched by GameId.
        var gameReviewCounts = await _dbContext.GameReviews
            .Where(r => !r.IsRemoved && gameIds.Contains(r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

        var postReviewCounts = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved && gameIds.Contains(r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

        // Active characters for [X/Y] tooltip — feeds game/room tooltips.
        var activeCharacterData = await _dbContext.Characters
            .Where(c => gameIds.Contains(c.GameId) && c.Status == CharacterStatus.Active && !c.IsNpc)
            .Select(c => new { c.GameId, c.Name, OwnerUsername = c.Author!.Username })
            .ToListAsync(ct);

        var activeCharactersMap = activeCharacterData
            .GroupBy(c => c.GameId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => new ActiveCharacterInfo
                {
                    Name = c.Name,
                    OwnerUsername = c.OwnerUsername
                }).ToList());

        foreach (var game in games)
        {
            game.GameReviewsCount = gameReviewCounts.GetValueOrDefault(game.Id, 0);
            game.PostReviewsCount = postReviewCounts.GetValueOrDefault(game.Id, 0);
            game.ActiveCharacters = activeCharactersMap.GetValueOrDefault(game.Id, []);
        }

        await FillTurnMarkersAsync(games, gameIds, userId, ct);
    }

    /// <summary>
    /// Populate the turn marker — whether the game waits for a post from this
    /// viewer, and for which of his characters.
    /// </summary>
    /// <remarks>
    /// The room selection asked of the pendencies table: one statement for the page,
    /// a row only for this viewer, none for an anonymous read.
    /// </remarks>
    private async Task FillTurnMarkersAsync<T>(
        IReadOnlyList<T> games,
        HashSet<Guid> gameIds,
        Guid userId,
        CancellationToken ct) where T : GameDto
    {
        if (userId == Guid.Empty)
        {
            foreach (var game in games)
            {
                game.AwaitsViewerTurn = false;
                game.AwaitedCharacterNames = [];
            }

            return;
        }

        var awaited = await _dbContext.PostPendencies
            .AsNoTracking()
            .Where(p => p.WaitingForUserId == userId &&
                        p.FulfilledUtc == null &&
                        // A removed room is off the room list, so the expectation
                        // inside it is off every screen that reads one.
                        !p.Room.IsRemoved &&
                        gameIds.Contains(p.Room.GameId))
            .Where(PostPendencyFilters.Genuine)
            .OrderBy(p => p.CreatedUtc)
            .ThenBy(p => p.PendencyId)
            .Select(p => new { p.Room.GameId, CharacterName = p.Character.Name })
            .ToListAsync(ct);

        var awaitedByGame = awaited
            .GroupBy(p => p.GameId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.CharacterName).Distinct().ToList());

        foreach (var game in games)
        {
            var names = awaitedByGame.GetValueOrDefault(game.Id, []);
            game.AwaitedCharacterNames = names;
            game.AwaitsViewerTurn = names.Count > 0;
        }
    }
}
