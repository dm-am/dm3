using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;

namespace DM.Web.API.Features.Personal.Invitations;

public class PersonalInvitationApiService
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

    public async Task<IEnumerable<ReceivedInvitation>> GetMyInvitations()
    {
        var games = (await _gameMemberService.GetUserInvitations()).Select(inv => new ReceivedInvitation
        {
            Id = inv.TokenId,
            EntityId = inv.GameId,
            EntityType = "game",
            EntityTitle = inv.GameTitle,
            InviterUsername = inv.InvitedBy.Username,
            Type = inv.TargetRole.ToApiString(),
            CreatedUtc = inv.CreatedUtc
        });
        var blogs = (await _blogInvitationService.GetUserPendingInvitations()).Select(inv => new ReceivedInvitation
        {
            Id = inv.TokenId,
            EntityId = inv.BlogId,
            EntityType = "blog",
            EntityTitle = inv.BlogTitle,
            InviterUsername = inv.InvitedBy.Username,
            Type = inv.TargetRole.ToApiString(),
            CreatedUtc = inv.CreatedUtc
        });

        return games.Concat(blogs).OrderByDescending(i => i.CreatedUtc).ToList();
    }

    public Task AcceptInvitation(Guid tokenId) =>
        Settle(tokenId, id => _gameMemberService.AcceptInvitation(id), _blogInvitationService.AcceptInvitation);

    public Task RejectInvitation(Guid tokenId) =>
        Settle(tokenId, id => _gameMemberService.RejectInvitation(id), _blogInvitationService.RejectInvitation);

    // The token does not say whose it is: the game invitations are asked first.
    private async Task Settle(Guid tokenId, Func<Guid, Task> inGame, Func<Guid, Task> inBlog)
    {
        if ((await _gameMemberService.GetUserInvitations()).Any(i => i.TokenId == tokenId))
        {
            await inGame(tokenId);
            return;
        }

        if ((await _blogInvitationService.GetUserPendingInvitations()).Any(i => i.TokenId == tokenId))
        {
            await inBlog(tokenId);
            return;
        }

        throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFoundOrSettled);
    }
}
