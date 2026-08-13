using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Events;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Domain.Blog.Features.Invitations;

/// <inheritdoc />
internal class BlogInvitationService : IBlogInvitationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IBlogInvitationRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IBlogService _blogService;
    private readonly IBlogSubscriptionService _subscriptionService;
    private readonly IUserLookupService _userLookupService;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BlogInvitationService(
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IBlogInvitationRepository repository,
        IIntentionManager intentionManager,
        IBlogService blogService,
        IBlogSubscriptionService subscriptionService,
        IUserLookupService userLookupService,
        IUserBlacklistChecker userBlacklistChecker,
        IEventProducer eventProducer,
        IDateTimeProvider dateTimeProvider)
    {
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _repository = repository;
        _intentionManager = intentionManager;
        _blogService = blogService;
        _subscriptionService = subscriptionService;
        _userLookupService = userLookupService;
        _userBlacklistChecker = userBlacklistChecker;
        _eventProducer = eventProducer;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<BlogInvitation> InviteAssistant(Guid blogId, string username)
    {
        var blog = await _blogService.GetBlogAsync(blogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.InviteAssistant, blog);

        var user = await _userLookupService.GetAsync(username);
        var userId = user.UserId;

        await ValidateInvitation(blog, userId);

        return await CreateInvitation(blogId, userId, username, TokenType.BlogAssistantInvitation);
    }

    /// <inheritdoc />
    public async Task<BlogInvitation> InviteReader(Guid blogId, string username)
    {
        var blog = await _blogService.GetBlogAsync(blogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.InviteReader, blog);

        var user = await _userLookupService.GetAsync(username);
        var userId = user.UserId;

        await ValidateInvitation(blog, userId);

        return await CreateInvitation(blogId, userId, username, TokenType.BlogReaderInvitation);
    }

    private async Task ValidateInvitation(BlogDto blog, Guid userId)
    {
        // Check content blacklist
        if (blog.BlacklistedUserIds.Contains(userId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.CannotInviteBlacklistedUser);
        }

        // Check personal blacklist - cannot invite someone you've blocked
        var currentUserId = _identityProvider.Current.User.UserId;
        if (await _userBlacklistChecker.IsBlockedAsync(currentUserId, userId))
        {
            throw new HttpException(HttpStatusCode.UnprocessableEntity, RefusalMessage.CannotInviteBlockedUser);
        }
    }

    private async Task<BlogInvitation> CreateInvitation(Guid blogId, Guid userId, string username, TokenType type)
    {
        // Invalidate existing invitations of same type for this user
        var existingInvites = await _repository.FindInvitations(blogId, userId, type);
        var tokenId = _guidFactory.Create();
        var now = _dateTimeProvider.Now;

        var createInvitation = new CreateBlogInvitationEntity
        {
            TokenId = tokenId,
            UserId = userId,
            TokenType = type,
            BlogId = blogId,
            CreatedUtc = now
        };

        await _repository.InvalidateAndCreate(existingInvites, createInvitation);

        await _eventProducer.SendAsync(EventType.BlogInvitationCreated, tokenId);

        var currentUser = _identityProvider.Current.User;
        var blog = await _blogService.GetBlogAsync(blogId);
        return new BlogInvitation
        {
            TokenId = tokenId,
            BlogId = blogId,
            BlogTitle = blog.Title,
            InvitedUser = new GeneralUser { UserId = userId, Username = username },
            InvitedBy = new GeneralUser { UserId = currentUser.UserId, Username = currentUser.Username },
            TargetRole = type == TokenType.BlogAssistantInvitation ? BlogRole.Assistant : BlogRole.Reader,
            CreatedUtc = now,
            ExpiresUtc = InvitationPolicy.ExpiresAt(now)
        };
    }

    /// <inheritdoc />
    public Task AcceptInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, true);

    /// <inheritdoc />
    public Task RejectInvitation(Guid tokenId) =>
        ProcessInvitation(tokenId, false);

    private async Task ProcessInvitation(Guid tokenId, bool accept)
    {
        var userId = _identityProvider.Current.User.UserId;
        var invitation = await _repository.GetInvitation(tokenId);

        // Validate invitation exists and matches current user
        if (invitation == null || invitation.InvitedUser.UserId != userId)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        // Check if it's a valid blog invitation type (Reader or Assistant)
        if (invitation.TargetRole != BlogRole.Assistant && invitation.TargetRole != BlogRole.Reader)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        // Check if invitation has expired
        if (_dateTimeProvider.Now > invitation.ExpiresUtc)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.InvitationExpired);
        }

        if (accept)
        {
            if (invitation.TargetRole == BlogRole.Assistant)
            {
                // The assistant row and the spent invitation go in together: written
                // separately, a refusal in between left the invitation live next to
                // somebody who already holds what it grants.
                await _repository.AcceptAssistantInvitation(new AddBlogAssistantEntity
                {
                    BlogId = invitation.BlogId,
                    UserId = userId,
                    JoinedUtc = _dateTimeProvider.Now
                }, tokenId);
            }
            else
            {
                // Straight to the subscription, not through BlogService.Subscribe:
                // that method is the public door and refuses a blog whose drafts
                // are private, which is exactly the blog an invitation is issued
                // for. Routed through it, a reader invitation to a private blog
                // could never be accepted — the invited user got 403 on the only
                // path that redeems it. The game side redeems its own reader
                // invitation the same way, past its own public door.
                //
                // The subscription first and the token second: subscribing is
                // idempotent, so a break between the two is repaired by following the
                // same link again, while the other order would spend the invitation
                // and leave nothing for the second attempt to do.
                await _subscriptionService.SubscribeAsync(invitation.BlogId);
                await _repository.Invalidate(tokenId);
            }

            await _eventProducer.SendAsync(EventType.BlogInvitationAccepted, tokenId);
        }
        else
        {
            await _repository.Invalidate(tokenId);
            await _eventProducer.SendAsync(EventType.BlogInvitationRejected, tokenId);
        }
    }

    /// <inheritdoc />
    public async Task CancelInvitation(Guid tokenId)
    {
        var invitation = await _repository.GetInvitation(tokenId);
        if (invitation == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        var blog = await _blogService.GetBlogAsync(invitation.BlogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.CancelInvitation, blog);

        await _repository.Invalidate(tokenId);

        await _eventProducer.SendAsync(EventType.BlogInvitationCancelled, tokenId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetPendingInvitations(Guid blogId)
    {
        var blog = await _blogService.GetBlogAsync(blogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        return await _repository.GetPendingInvitations(blogId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetUserPendingInvitations()
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserPendingInvitations(userId);
    }
}
