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
using DM.Web.API.Features.Community.Users;
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
        var mappedUsers = users.Select(MapToDto);

        if (string.IsNullOrEmpty(role))
        {
            return mappedUsers;
        }

        // Apply role filter
        return role.ToLowerInvariant() switch
        {
            "master" => mappedUsers.Where(u => u.Role == "master"),
            "assistant" => mappedUsers.Where(u => u.Role == "assistant"),
            "mentor" => mappedUsers.Where(u => u.Role == "mentor"),
            "player" => mappedUsers.Where(u => u.Role == "player"),
            "applicant" => mappedUsers.Where(u => u.Role == "applicant"),
            "reader" => mappedUsers.Where(u => u.Role == "reader"),
            "formerplayer" => mappedUsers.Where(u => u.Role == "formerPlayer"),
            _ => mappedUsers
        };
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
        var subscribers = await _subscriptionService.GetReaders(gameId);
        return subscribers.Select(u => MapUserToGameUser(u, GameRole.Reader));
    }

    /// <inheritdoc />
    public async Task<GameUser> Subscribe(Guid gameId)
    {
        await _subscriptionService.Subscribe(gameId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var subscribers = await _subscriptionService.GetReaders(gameId);
        var currentUser = subscribers.First(s => s.UserId == currentUserId);
        return MapUserToGameUser(currentUser, GameRole.Reader);
    }

    /// <inheritdoc />
    public Task Unsubscribe(Guid gameId)
    {
        return _subscriptionService.Unsubscribe(gameId);
    }

    // Note: RemoveReader is not provided - readers can only unsubscribe themselves
    // Use blacklist to prevent problematic users from accessing content

    #endregion

    #region Mapping

    private static GameUser MapToDto(DomainGameUser user)
    {
        return new GameUser
        {
            User = new User
            {
                Id = user.User.UserId,
                Username = user.User.Username
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
            User = new User
            {
                Id = user.UserId,
                Username = user.Username
            },
            Role = role.ToApiString(),
            JoinedUtc = DateTimeOffset.MinValue // Not available from GeneralUser
        };
    }

    #endregion
}
