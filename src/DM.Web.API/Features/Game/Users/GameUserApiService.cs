using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using DomainGameUser = DM.Domain.Game.Features.Games.GameUser;

namespace DM.Web.API.Features.Game.Users;

public class GameUserApiService
{
    private readonly IGameInvitationService _invitationService;
    private readonly IGameSubscriptionService _subscriptionService;
    private readonly IGameService _gameService;
    private readonly IIdentityProvider _identityProvider;

    public GameUserApiService(
        IGameInvitationService invitationService,
        IGameSubscriptionService subscriptionService,
        IGameService gameService,
        IIdentityProvider identityProvider)
    {
        _invitationService = invitationService;
        _subscriptionService = subscriptionService;
        _gameService = gameService;
        _identityProvider = identityProvider;
    }

    public async Task<IEnumerable<GameUser>> GetUsers(Guid gameId, GameRole? role = null)
    {
        var users = await _invitationService.GetUsers(gameId);

        // Filtered on the role itself, before it is rendered.
        if (role.HasValue)
        {
            users = users.Where(u => u.Role == role.Value);
        }

        return users.Select(MapToDto);
    }

    public async Task<IEnumerable<GameUser>> GetAssistants(Guid gameId)
    {
        var assistants = await _gameService.GetAssistantsAsync(gameId);
        return assistants.Select(u => AsGameUser(UserRefMappers.ToUserRef(u), GameRole.Assistant));
    }

    public Task RemoveAssistantByUsername(Guid gameId, string username) =>
        _gameService.RemoveAssistantAsync(gameId, username);

    public async Task<IEnumerable<GameUser>> GetReaders(Guid gameId)
    {
        await _gameService.GetAsync(gameId); // Validate game exists
        var subscribers = await _subscriptionService.GetSubscribersAsync(gameId);
        return subscribers.Select(u => AsGameUser(UserRefMappers.ToUserRef(u), GameRole.Reader));
    }

    public async Task<GameUser> Subscribe(Guid gameId)
    {
        // Read first: a game hidden from the reader answers 404, as the list of readers does.
        await _gameService.GetAsync(gameId);
        await _subscriptionService.SubscribeAsync(gameId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var subscribers = await _subscriptionService.GetSubscribersAsync(gameId);
        var currentUser = subscribers.First(s => s.UserId == currentUserId);
        return AsGameUser(UserRefMappers.ToUserRef(currentUser), GameRole.Reader);
    }

    public Task Unsubscribe(Guid gameId) => _subscriptionService.UnsubscribeAsync(gameId);

    private static GameUser MapToDto(DomainGameUser user)
    {
        return new GameUser
        {
            User = UserRefMappers.ToUserRef(user.User),
            Role = user.Role.ToApiString(),
            JoinedUtc = user.JoinedUtc,
            CharacterId = user.CharacterId,
            CharacterName = user.CharacterName,
            CharacterStatus = user.CharacterStatus?.ToString().ToLowerInvariant()
        };
    }

    /// <summary>
    /// A roster line for somebody whose place in the game comes from the role
    /// alone, with no record of when they took it.
    /// </summary>
    private static GameUser AsGameUser(UserRef user, GameRole role) =>
        new()
        {
            User = user,
            Role = role.ToApiString(),
            JoinedUtc = DateTimeOffset.MinValue
        };
}
