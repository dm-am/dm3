using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Extensions;
using DM.Web.API.Shared.Dto;
using DomainGameUser = DM.Domain.Game.Features.Games.GameUser;

namespace DM.Web.API.Features.Game.Users;

/// <inheritdoc />
internal class GameUserApiService : IGameUserApiService
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

    #region Users

    /// <inheritdoc />
    public async Task<IEnumerable<GameUser>> GetUsers(Guid gameId, string? role = null)
    {
        var users = await _invitationService.GetUsers(gameId);

        // Filtered on the role itself, before it is rendered. The filter compared
        // the rendered string against literals it wrote out a second time, and
        // three of them - "mentor", "applicant", "formerPlayer" - were values
        // ToApiString never produced; the model has no former player at all. An
        // unrecognised value returns everybody, as on the blog side.
        if (!string.IsNullOrEmpty(role) && GameRoleExtensions.TryParseApiString(role, out var wanted))
        {
            users = users.Where(u => u.Role == wanted);
        }

        return users.Select(MapToDto);
    }

    /// <inheritdoc />
    public Task RemoveUser(Guid gameId, Guid userId)
    {
        return _invitationService.RemoveUser(gameId, userId);
    }

    #endregion

    #region Assistants

    /// <inheritdoc />
    public async Task<IEnumerable<GameUser>> GetAssistants(Guid gameId)
    {
        var assistants = await _gameService.GetAssistantsAsync(gameId);
        return assistants.Select(u => MapUserToGameUser(u, GameRole.Assistant));
    }

    /// <inheritdoc />
    public Task RemoveAssistantByUsername(Guid gameId, string username)
    {
        return _gameService.RemoveAssistantAsync(gameId, username);
    }

    #endregion

    #region Readers

    /// <inheritdoc />
    public async Task<IEnumerable<GameUser>> GetReaders(Guid gameId)
    {
        await _gameService.GetAsync(gameId); // Validate game exists
        var subscribers = await _subscriptionService.GetSubscribersAsync(gameId);
        return subscribers.Select(u => MapUserToGameUser(u, GameRole.Reader));
    }

    /// <inheritdoc />
    public async Task<GameUser> Subscribe(Guid gameId)
    {
        await _subscriptionService.SubscribeAsync(gameId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var subscribers = await _subscriptionService.GetSubscribersAsync(gameId);
        var currentUser = subscribers.First(s => s.UserId == currentUserId);
        return MapUserToGameUser(currentUser, GameRole.Reader);
    }

    /// <inheritdoc />
    public Task Unsubscribe(Guid gameId)
    {
        return _subscriptionService.UnsubscribeAsync(gameId);
    }

    // Note: RemoveReader is not provided - readers can only unsubscribe themselves
    // Use blacklist to prevent problematic users from accessing content

    #endregion

    #region Mapping

    private static GameUser MapToDto(DomainGameUser user)
    {
        return new GameUser
        {
            User = new UserRef
            {
                Id = user.User.UserId,
                Username = user.User.Username,
                LastActivityUtc = user.User.LastActivityUtc
            },
            Role = user.Role.ToApiString(),
            JoinedUtc = user.JoinedUtc,
            CharacterId = user.CharacterId,
            CharacterName = user.CharacterName,
            CharacterStatus = user.CharacterStatus?.ToString().ToLowerInvariant()
        };
    }

    private static GameUser MapUserToGameUser(GeneralUser user, GameRole role)
    {
        return new GameUser
        {
            User = new UserRef
            {
                Id = user.UserId,
                Username = user.Username,
                LastActivityUtc = user.LastActivityUtc
            },
            Role = role.ToApiString(),
            JoinedUtc = DateTimeOffset.MinValue // Not available from GeneralUser
        };
    }

    #endregion
}
