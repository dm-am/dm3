using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;

namespace DM.Web.API.Features.Personal.Invitations;

/// <inheritdoc />
internal class PersonalInvitationApiService : IPersonalInvitationApiService
{
    private readonly IGameInvitationService _gameMemberService;
    private readonly IBlogInvitationService _blogInvitationService;

    public PersonalInvitationApiService(
        IGameInvitationService gameMemberService,
        IBlogInvitationService blogInvitationService)
    {
        _gameMemberService = gameMemberService;
        _blogInvitationService = blogInvitationService;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ReceivedInvitation>> GetMyInvitations()
    {
        var gameInvitations = await _gameMemberService.GetUserInvitations();
        var blogInvitations = await _blogInvitationService.GetUserPendingInvitations();

        var result = new List<ReceivedInvitation>();

        foreach (var inv in gameInvitations)
        {
            result.Add(new ReceivedInvitation
            {
                Id = inv.TokenId,
                EntityId = inv.GameId,
                EntityType = "game",
                EntityTitle = inv.GameTitle,
                InviterUsername = inv.InvitedBy.Username,
                Type = inv.TargetRole.ToApiString(),
                CreatedUtc = inv.CreatedUtc
            });
        }

        foreach (var inv in blogInvitations)
        {
            result.Add(new ReceivedInvitation
            {
                Id = inv.TokenId,
                EntityId = inv.BlogId,
                EntityType = "blog",
                EntityTitle = inv.BlogTitle,
                InviterUsername = inv.InvitedBy.Username,
                Type = inv.TargetRole.ToApiString(),
                CreatedUtc = inv.CreatedUtc
            });
        }

        return result.OrderByDescending(i => i.CreatedUtc);
    }

    /// <inheritdoc />
    public async Task AcceptInvitation(Guid tokenId)
    {
        // Try game invitations first (unified service handles all game invitation types)
        var gameInvitations = await _gameMemberService.GetUserInvitations();
        if (gameInvitations.Any(i => i.TokenId == tokenId))
        {
            await _gameMemberService.AcceptInvitation(tokenId);
            return;
        }

        // Try blog invitations (unified service handles all blog invitation types)
        var blogInvitations = await _blogInvitationService.GetUserPendingInvitations();
        if (blogInvitations.Any(i => i.TokenId == tokenId))
        {
            await _blogInvitationService.AcceptInvitation(tokenId);
            return;
        }

        throw new DM.Domain.Core.Exceptions.HttpException(
            System.Net.HttpStatusCode.NotFound,
            "Invitation not found or already processed");
    }

    /// <inheritdoc />
    public async Task RejectInvitation(Guid tokenId)
    {
        // Try game invitations first (unified service handles all game invitation types)
        var gameInvitations = await _gameMemberService.GetUserInvitations();
        if (gameInvitations.Any(i => i.TokenId == tokenId))
        {
            await _gameMemberService.RejectInvitation(tokenId);
            return;
        }

        // Try blog invitations (unified service handles all blog invitation types)
        var blogInvitations = await _blogInvitationService.GetUserPendingInvitations();
        if (blogInvitations.Any(i => i.TokenId == tokenId))
        {
            await _blogInvitationService.RejectInvitation(tokenId);
            return;
        }

        throw new DM.Domain.Core.Exceptions.HttpException(
            System.Net.HttpStatusCode.NotFound,
            "Invitation not found or already processed");
    }
}
