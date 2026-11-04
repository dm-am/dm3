using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Web.API.Shared.Dto;
using DomainGameInvitation = DM.Domain.Game.Features.Invitations.GameInvitation;

namespace DM.Web.API.Features.Game.Invitations;

public class GameInvitationApiService
{
    private readonly IGameInvitationService _memberService;

    public GameInvitationApiService(IGameInvitationService memberService)
    {
        _memberService = memberService;
    }

    public async Task<IEnumerable<GameInvitation>> GetGameInvitations(Guid gameId)
    {
        var invitations = await _memberService.GetPendingInvitations(gameId);
        return invitations.Select(MapToDto);
    }

    public async Task<GameInvitation> InvitePlayer(Guid gameId, string username)
    {
        var invitation = await _memberService.InvitePlayer(gameId, username);
        return MapToDto(invitation);
    }

    public async Task<GameInvitation> InviteReader(Guid gameId, string username)
    {
        var invitation = await _memberService.InviteReader(gameId, username);
        return MapToDto(invitation);
    }

    public async Task<GameInvitation> InviteAssistant(Guid gameId, string username)
    {
        var invitation = await _memberService.InviteAssistant(gameId, username);
        return MapToDto(invitation);
    }

    public Task CancelInvitation(Guid tokenId) => _memberService.CancelInvitation(tokenId);

    private static GameInvitation MapToDto(DomainGameInvitation info)
    {
        return new GameInvitation
        {
            Id = info.TokenId,
            GameId = info.GameId,
            GameTitle = info.GameTitle,
            InvitedUser = UserRefMappers.ToUserRef(info.InvitedUser),
            InviterUsername = info.InvitedBy.Username,
            Type = info.TargetRole.ToApiString(),
            CreatedUtc = info.CreatedUtc,
            ExpiresUtc = info.ExpiresUtc
        };
    }
}
