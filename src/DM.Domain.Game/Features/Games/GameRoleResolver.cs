using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class GameRoleResolver : IGameRoleResolver
{
    private readonly IGameRepository _gameRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public GameRoleResolver(
        IGameRepository gameRepository,
        ISubscriptionRepository subscriptionRepository)
    {
        _gameRepository = gameRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    /// <inheritdoc />
    public async Task<GameRole> GetRoleAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return GameRole.None;

        var game = await _gameRepository.GetGame(gameId, userId);
        if (game == null)
            return GameRole.None;

        // Check master/assistant roles first (highest privilege)
        if (game.Author.UserId == userId)
            return GameRole.Master;

        if (game.Assistants.Any(a => a.UserId == userId))
            return GameRole.Assistant;

        if (game.Mentor?.UserId == userId)
            return GameRole.Mentor;

        // Check player role
        if (game.ActiveCharacterUserIds.Contains(userId))
            return GameRole.Player;

        // Check reader (subscription-based)
        var subscription = await _subscriptionRepository.FindAsync(
            userId, SubscriptionTargetType.Game, gameId, ct);
        if (subscription != null)
            return GameRole.Reader;

        // Note: Applicant role (pending character) requires character status check
        // which is not currently available in the Game DTO
        // For now, we don't return Applicant - can be enhanced later

        return GameRole.None;
    }

    /// <inheritdoc />
    public GameRole GetRole(GameModel game, Guid userId)
    {
        if (userId == Guid.Empty || game == null)
            return GameRole.None;

        // Check master/assistant roles first (highest privilege)
        if (game.Author.UserId == userId)
            return GameRole.Master;

        if (game.Assistants.Any(a => a.UserId == userId))
            return GameRole.Assistant;

        if (game.Mentor?.UserId == userId)
            return GameRole.Mentor;

        // Check player role
        if (game.ActiveCharacterUserIds.Contains(userId))
            return GameRole.Player;

        // Check reader (requires ReaderUserIds to be populated from Subscriptions)
        if (game.ReaderUserIds.Contains(userId))
            return GameRole.Reader;

        // Note: Applicant role detection would require pending character data
        // which is not in the Game DTO

        return GameRole.None;
    }
}
