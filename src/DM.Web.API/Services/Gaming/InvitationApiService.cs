using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.Gaming.BusinessProcesses.Games.Invitations;
using DM.Services.Gaming.BusinessProcesses.Games.Shared;
using DM.Web.API.Dto.Games;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Gaming;

/// <inheritdoc />
internal class InvitationApiService : IInvitationApiService
{
    private readonly IInvitationService _invitationService;
    private readonly IUserRepository _userRepository;

    /// <inheritdoc />
    public InvitationApiService(
        IInvitationService invitationService,
        IUserRepository userRepository)
    {
        _invitationService = invitationService;
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Invitation>> GetGameInvitations(Guid gameId)
    {
        var invitations = await _invitationService.GetPendingInvitations(gameId);
        return invitations.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Invitation>> GetUserInvitations()
    {
        var invitations = await _invitationService.GetUserPendingInvitations();
        return invitations.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<Invitation> InvitePlayer(Guid gameId, string login)
    {
        var (exists, userId) = await _userRepository.FindUserId(login);
        if (!exists)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"User '{login}' not found");
        }

        var token = await _invitationService.CreatePlayerInvitation(gameId, userId);
        var invitation = await GetInvitationInfo(token.TokenId);
        return invitation;
    }

    /// <inheritdoc />
    public async Task<Invitation> InviteReader(Guid gameId, string login)
    {
        var (exists, userId) = await _userRepository.FindUserId(login);
        if (!exists)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"User '{login}' not found");
        }

        var token = await _invitationService.CreateReaderInvitation(gameId, userId);
        var invitation = await GetInvitationInfo(token.TokenId);
        return invitation;
    }

    /// <inheritdoc />
    public Task CancelInvitation(Guid tokenId)
    {
        return _invitationService.CancelInvitation(tokenId);
    }

    /// <inheritdoc />
    public async Task AcceptInvitation(Guid tokenId)
    {
        var invitations = await _invitationService.GetUserPendingInvitations();
        var invitation = invitations.FirstOrDefault(i => i.TokenId == tokenId);
        if (invitation == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Invitation not found or already processed");
        }

        switch (invitation.Type)
        {
            case TokenType.PlayerInvitation:
                await _invitationService.AcceptPlayerInvitation(tokenId);
                break;
            case TokenType.ReaderInvitation:
                await _invitationService.AcceptReaderInvitation(tokenId);
                break;
            case TokenType.AssistantAssignment:
                throw new HttpException(HttpStatusCode.BadRequest,
                    "Use the assistant assignment endpoint to accept this invitation");
            default:
                throw new HttpException(HttpStatusCode.BadRequest, "Unknown invitation type");
        }
    }

    /// <inheritdoc />
    public async Task RejectInvitation(Guid tokenId)
    {
        var invitations = await _invitationService.GetUserPendingInvitations();
        var invitation = invitations.FirstOrDefault(i => i.TokenId == tokenId);
        if (invitation == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Invitation not found or already processed");
        }

        switch (invitation.Type)
        {
            case TokenType.PlayerInvitation:
                await _invitationService.RejectPlayerInvitation(tokenId);
                break;
            case TokenType.ReaderInvitation:
                await _invitationService.RejectReaderInvitation(tokenId);
                break;
            case TokenType.AssistantAssignment:
                throw new HttpException(HttpStatusCode.BadRequest,
                    "Use the assistant assignment endpoint to reject this invitation");
            default:
                throw new HttpException(HttpStatusCode.BadRequest, "Unknown invitation type");
        }
    }

    private async Task<Invitation> GetInvitationInfo(Guid tokenId)
    {
        var invitations = await _invitationService.GetUserPendingInvitations();
        var info = invitations.FirstOrDefault(i => i.TokenId == tokenId);
        return info != null ? MapToDto(info) : null;
    }

    private static Invitation MapToDto(InvitationInfo info)
    {
        return new Invitation
        {
            Id = info.TokenId,
            GameId = info.GameId,
            GameTitle = info.GameTitle,
            InvitedUser = new Dto.Users.User { Login = info.UserLogin },
            InviterLogin = info.InviterLogin,
            Type = info.Type switch
            {
                TokenType.AssistantAssignment => "assistant",
                TokenType.PlayerInvitation => "player",
                TokenType.ReaderInvitation => "reader",
                _ => "unknown"
            },
            CreatedUtc = info.CreatedUtc
        };
    }
}
