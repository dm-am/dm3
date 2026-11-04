using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Invitations;
using DM.Web.API.Shared.Dto;
using DomainBlogInvitation = DM.Domain.Blog.Features.Invitations.BlogInvitation;

namespace DM.Web.API.Features.Blog.Invitations;

public class BlogInvitationApiService
{
    private readonly IBlogInvitationService _invitationService;

    public BlogInvitationApiService(IBlogInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    public async Task<BlogInvitation> CreateAssistantInvitation(Guid blogId, string username)
    {
        var invitation = await _invitationService.InviteAssistant(blogId, username);
        return MapToDto(invitation);
    }

    public async Task<BlogInvitation> CreateReaderInvitation(Guid blogId, string username)
    {
        var invitation = await _invitationService.InviteReader(blogId, username);
        return MapToDto(invitation);
    }

    public Task CancelInvitation(Guid tokenId) =>
        _invitationService.CancelInvitation(tokenId);

    public async Task<IEnumerable<BlogInvitation>> GetBlogInvitations(Guid blogId)
    {
        var invitations = await _invitationService.GetPendingInvitations(blogId);
        return invitations.Select(MapToDto);
    }

    private static BlogInvitation MapToDto(DomainBlogInvitation invitation) => new()
    {
        Id = invitation.TokenId,
        BlogId = invitation.BlogId,
        BlogTitle = invitation.BlogTitle,
        InvitedUser = UserRefMappers.ToUserRef(invitation.InvitedUser),
        InviterUsername = invitation.InvitedBy.Username,
        Type = invitation.TargetRole.ToApiString(),
        CreatedUtc = invitation.CreatedUtc,
        ExpiresUtc = invitation.ExpiresUtc
    };
}
