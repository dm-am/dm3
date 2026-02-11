using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Tokens;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Blogs;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;

namespace DM.Services.Community.BusinessProcesses.Blogs.Invitations;

/// <inheritdoc />
internal class BlogInvitationService : IBlogInvitationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ITokenFactory _tokenFactory;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IBlogInvitationRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IBlogService _blogService;

    /// <inheritdoc />
    public BlogInvitationService(
        IIdentityProvider identityProvider,
        ITokenFactory tokenFactory,
        IUpdateBuilderFactory updateBuilderFactory,
        IBlogInvitationRepository repository,
        IIntentionManager intentionManager,
        IBlogService blogService)
    {
        _identityProvider = identityProvider;
        _tokenFactory = tokenFactory;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _intentionManager = intentionManager;
        _blogService = blogService;
    }

    /// <inheritdoc />
    public async Task<Token> CreateAssistantInvitation(Guid blogId, Guid userId)
    {
        var blog = await _blogService.GetBlog(blogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.ManageParticipants, blog);

        return await CreateInvitation(blogId, userId, TokenType.BlogAssistantInvitation);
    }

    /// <inheritdoc />
    public async Task<Token> CreateReaderInvitation(Guid blogId, Guid userId)
    {
        var blog = await _blogService.GetBlog(blogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.ManageParticipants, blog);

        return await CreateInvitation(blogId, userId, TokenType.BlogReaderInvitation);
    }

    private async Task<Token> CreateInvitation(Guid blogId, Guid userId, TokenType type)
    {
        // Invalidate existing invitations of same type for this user
        var existingInvites = await _repository.FindInvitations(blogId, userId, type);
        var updates = existingInvites.Select(id =>
            _updateBuilderFactory.Create<Token>(id).Field(t => t.IsRemoved, true));

        var token = _tokenFactory.Create(userId, blogId, type);
        await _repository.InvalidateAndCreate(updates, token);

        return token;
    }

    /// <inheritdoc />
    public Task AcceptAssistantInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, TokenType.BlogAssistantInvitation, true);

    /// <inheritdoc />
    public Task RejectAssistantInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, TokenType.BlogAssistantInvitation, false);

    /// <inheritdoc />
    public Task AcceptReaderInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, TokenType.BlogReaderInvitation, true);

    /// <inheritdoc />
    public Task RejectReaderInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, TokenType.BlogReaderInvitation, false);

    private async Task ProcessInvitation(Guid tokenId, TokenType type, bool accept)
    {
        var userId = _identityProvider.Current.User.UserId;
        var blogId = await _repository.FindBlogByToken(tokenId, userId, type);
        if (!blogId.HasValue)
        {
            throw new HttpException(HttpStatusCode.Gone,
                "Invitation is invalid or expired");
        }

        // Mark token as used
        var updateToken = _updateBuilderFactory.Create<Token>(tokenId).Field(t => t.IsRemoved, true);
        await _repository.Update(updateToken);

        if (accept)
        {
            // Add user as participant
            var role = type == TokenType.BlogAssistantInvitation
                ? BlogParticipation.Assistant
                : BlogParticipation.Reader;

            await _blogService.AddParticipant(blogId.Value, userId, role);
        }
    }

    /// <inheritdoc />
    public async Task CancelInvitation(Guid tokenId)
    {
        var invitation = await _repository.GetInvitation(tokenId);
        if (invitation == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Invitation not found");
        }

        var blog = await _blogService.GetBlog(invitation.BlogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.ManageParticipants, blog);

        var updateToken = _updateBuilderFactory.Create<Token>(tokenId).Field(t => t.IsRemoved, true);
        await _repository.Update(updateToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitationInfo>> GetPendingInvitations(Guid blogId)
    {
        var blog = await _blogService.GetBlog(blogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        return await _repository.GetPendingInvitations(blogId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitationInfo>> GetUserPendingInvitations()
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserPendingInvitations(userId);
    }
}
