using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Blogs.Invitations;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Web.API.Dto.Blogs;
using DM.Web.API.Dto.Contracts;

namespace DM.Web.API.Services.Blog;

/// <inheritdoc />
internal class BlogInvitationApiService : IBlogInvitationApiService
{
    private readonly IBlogInvitationService _invitationService;
    private readonly IUserReadingService _userService;

    /// <inheritdoc />
    public BlogInvitationApiService(
        IBlogInvitationService invitationService,
        IUserReadingService userService)
    {
        _invitationService = invitationService;
        _userService = userService;
    }

    /// <inheritdoc />
    public async Task<Envelope<BlogInvitation>> CreateAssistantInvitation(Guid blogId, string login)
    {
        var user = await _userService.Get(login);
        var token = await _invitationService.CreateAssistantInvitation(blogId, user.UserId);
        return new Envelope<BlogInvitation>(MapToDto(token, login, "Assistant"));
    }

    /// <inheritdoc />
    public async Task<Envelope<BlogInvitation>> CreateReaderInvitation(Guid blogId, string login)
    {
        var user = await _userService.Get(login);
        var token = await _invitationService.CreateReaderInvitation(blogId, user.UserId);
        return new Envelope<BlogInvitation>(MapToDto(token, login, "Reader"));
    }

    /// <inheritdoc />
    public async Task AcceptInvitation(Guid tokenId)
    {
        // Try both types - one will work based on the actual token type
        try
        {
            await _invitationService.AcceptAssistantInvitation(tokenId);
        }
        catch (HttpException)
        {
            await _invitationService.AcceptReaderInvitation(tokenId);
        }
    }

    /// <inheritdoc />
    public async Task RejectInvitation(Guid tokenId)
    {
        try
        {
            await _invitationService.RejectAssistantInvitation(tokenId);
        }
        catch (HttpException)
        {
            await _invitationService.RejectReaderInvitation(tokenId);
        }
    }

    /// <inheritdoc />
    public Task CancelInvitation(Guid tokenId) =>
        _invitationService.CancelInvitation(tokenId);

    /// <inheritdoc />
    public async Task<ListEnvelope<BlogInvitation>> GetBlogInvitations(Guid blogId)
    {
        var invitations = await _invitationService.GetPendingInvitations(blogId);
        var dtos = invitations.Select(MapToDto).ToList();
        return new ListEnvelope<BlogInvitation>(dtos);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<BlogInvitation>> GetMyInvitations()
    {
        var invitations = await _invitationService.GetUserPendingInvitations();
        var dtos = invitations.Select(MapToDto).ToList();
        return new ListEnvelope<BlogInvitation>(dtos);
    }

    private static BlogInvitation MapToDto(Token token, string login, string role) => new()
    {
        TokenId = token.TokenId,
        BlogId = token.EntityId ?? Guid.Empty,
        InvitedUserLogin = login,
        Role = role,
        CreatedAt = token.CreatedUtc
    };

    private static BlogInvitation MapToDto(BlogInvitationInfo info) => new()
    {
        TokenId = info.TokenId,
        BlogId = info.BlogId,
        BlogTitle = info.BlogTitle,
        InvitedUserLogin = info.UserLogin,
        Role = info.Type == TokenType.BlogAssistantInvitation ? "Assistant" : "Reader",
        CreatedAt = info.CreatedUtc
    };
}
