using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Invitations;
using DM.Web.API.Shared.Dto;
using DomainBlogInvitation = DM.Domain.Blog.Features.Invitations.BlogInvitation;

namespace DM.Web.API.Features.Blog.Invitations;

/// <inheritdoc />
internal class BlogInvitationApiService : IBlogInvitationApiService
{
    private readonly IBlogInvitationService _invitationService;

    /// <inheritdoc />
    public BlogInvitationApiService(IBlogInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <inheritdoc />
    public async Task<BlogInvitation> CreateAssistantInvitation(Guid blogId, string username)
    {
        var invitation = await _invitationService.InviteAssistant(blogId, username);
        return MapToDto(invitation);
    }

    /// <inheritdoc />
    public async Task<BlogInvitation> CreateReaderInvitation(Guid blogId, string username)
    {
        var invitation = await _invitationService.InviteReader(blogId, username);
        return MapToDto(invitation);
    }

    /// <inheritdoc />
    public Task CancelInvitation(Guid tokenId) =>
        _invitationService.CancelInvitation(tokenId);

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetBlogInvitations(Guid blogId)
    {
        var invitations = await _invitationService.GetPendingInvitations(blogId);
        return invitations.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetMyInvitations()
    {
        var invitations = await _invitationService.GetUserPendingInvitations();
        return invitations.Select(MapToDto);
    }

    /// <inheritdoc />
    public Task AcceptInvitation(Guid tokenId) =>
        _invitationService.AcceptInvitation(tokenId);

    /// <inheritdoc />
    public Task RejectInvitation(Guid tokenId) =>
        _invitationService.RejectInvitation(tokenId);

    private static BlogInvitation MapToDto(DomainBlogInvitation invitation) => new()
    {
        Id = invitation.TokenId,
        BlogId = invitation.BlogId,
        BlogTitle = invitation.BlogTitle,
        InvitedUser = new UserRef { Id = invitation.InvitedUser.UserId, Username = invitation.InvitedUser.Username, LastActivityUtc = invitation.InvitedUser.LastActivityUtc },
        InviterUsername = invitation.InvitedBy.Username,
        Type = invitation.TargetRole.ToString(),
        CreatedUtc = invitation.CreatedUtc,
        ExpiresUtc = invitation.ExpiresUtc
    };
}
