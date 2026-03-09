using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Web.API.Features.Community.Users;
using DomainGameInvitation = DM.Domain.Game.Features.Games.GameInvitation;

namespace DM.Web.API.Features.Game.Invitations;

/// <inheritdoc />
internal class GameInvitationApiService : IGameInvitationApiService
{
    private readonly IGameInvitationService _memberService;

    /// <inheritdoc />
    public GameInvitationApiService(IGameInvitationService memberService)
    {
        _memberService = memberService;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GameInvitation>> GetGameInvitations(Guid gameId)
    {
        var invitations = await _memberService.GetPendingInvitations(gameId);
        return invitations.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<GameInvitation> InvitePlayer(Guid gameId, string username)
    {
        var invitation = await _memberService.InvitePlayer(gameId, username);
        return MapToDto(invitation);
    }

    /// <inheritdoc />
    public async Task<GameInvitation> InviteReader(Guid gameId, string username)
    {
        var invitation = await _memberService.InviteReader(gameId, username);
        return MapToDto(invitation);
    }

    /// <inheritdoc />
    public async Task<GameInvitation> InviteAssistant(Guid gameId, string username)
    {
        var invitation = await _memberService.InviteAssistant(gameId, username);
        return MapToDto(invitation);
    }

    /// <inheritdoc />
    public Task CancelInvitation(Guid tokenId)
    {
        return _memberService.CancelInvitation(tokenId);
    }

    private static GameInvitation MapToDto(DomainGameInvitation info)
    {
        return new GameInvitation
        {
            Id = info.TokenId,
            GameId = info.GameId,
            GameTitle = info.GameTitle,
            InvitedUser = new User
            {
                Id = info.InvitedUser.UserId,
                Username = info.InvitedUser.Username
            },
            InviterUsername = info.InvitedBy.Username,
            Type = info.TargetRole.ToApiString(),
            CreatedUtc = info.CreatedUtc,
            ExpiresUtc = info.ExpiresUtc
        };
    }
}
