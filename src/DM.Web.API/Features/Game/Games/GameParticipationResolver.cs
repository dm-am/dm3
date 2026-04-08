using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using DM.Domain.Core.Identity;
using DomainGame = DM.Domain.Game.Features.Games.Game;
using DomainGameDetails = DM.Domain.Game.Features.Games.GameDetails;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Resolver for current user participation
/// </summary>
internal class GameParticipationResolver :
    IValueResolver<DomainGame, Game, IEnumerable<GameParticipation>>,
    IValueResolver<DomainGame, GameRef, IEnumerable<GameParticipation>>,
    IValueResolver<DomainGameDetails, Game, IEnumerable<GameParticipation>>
{
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public GameParticipationResolver(
        IIdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public IEnumerable<GameParticipation> Resolve(
        DomainGame source, Game destination, IEnumerable<GameParticipation> destMember, ResolutionContext context) =>
        Flatten(CalculateParticipation(source, _identityProvider.Current?.User?.UserId ?? Guid.Empty));

    /// <inheritdoc />
    public IEnumerable<GameParticipation> Resolve(
        DomainGame source, GameRef destination, IEnumerable<GameParticipation> destMember, ResolutionContext context) =>
        Flatten(CalculateParticipation(source, _identityProvider.Current?.User?.UserId ?? Guid.Empty));

    /// <inheritdoc />
    public IEnumerable<GameParticipation> Resolve(
        DomainGameDetails source, Game destination, IEnumerable<GameParticipation> destMember, ResolutionContext context) =>
        Flatten(CalculateParticipation(source, _identityProvider.Current?.User?.UserId ?? Guid.Empty));

    private static GameParticipation CalculateParticipation(DomainGame game, Guid userId)
    {
        var participation = GameParticipation.None;

        if (game.Master?.UserId == userId)
            participation |= GameParticipation.Owner | GameParticipation.Authority;

        if (game.Assistants?.Any(a => a.UserId == userId) == true)
            participation |= GameParticipation.Authority;

        if (game.PendingAssistant?.UserId == userId)
            participation |= GameParticipation.PendingAssistant;

        if (game.Players?.Any(p => p.UserId == userId) == true)
            participation |= GameParticipation.Player;

        if (game.SubscriberIds?.Contains(userId) == true)
            participation |= GameParticipation.Reader;

        if (game.Mentor?.UserId == userId)
            participation |= GameParticipation.Moderator;

        return participation;
    }

    private static IEnumerable<GameParticipation> Flatten(GameParticipation participation) =>
        Enum.GetValues(typeof(GameParticipation))
            .Cast<GameParticipation>()
            .Where(p => p != GameParticipation.None && (p & participation) == p);
}
