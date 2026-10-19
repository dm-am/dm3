using System;
using System.Collections.Generic;
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
    public Task<BlogInvitation> InviteAssistant(Guid blogId, string username) =>
        Invite(blogId, username, BlogIntention.InviteAssistant, TokenType.BlogAssistantInvitation);

    /// <inheritdoc />
    public Task<BlogInvitation> InviteReader(Guid blogId, string username) =>
        Invite(blogId, username, BlogIntention.InviteReader, TokenType.BlogReaderInvitation);

    private async Task<BlogInvitation> Invite(Guid blogId, string username, BlogIntention intention, TokenType type)
    {
        var blog = await _intentionManager.GetPermittedBlogAsync(_blogService, intention, blogId);

        var user = await _userLookupService.GetAsync(username);

        // The invitee's personal blacklist, as the game asks it; the blog's own list
        // closes comments only, and the inviter's restricts the people on it.
        var currentUserId = _identityProvider.Current.User.UserId;
        if (await _userBlacklistChecker.IsBlockedAsync(user.UserId, currentUserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.CannotInviteThisUser);
        }

        return await CreateInvitation(blogId, blog.Title, user.UserId, username, type);
    }

    private async Task<BlogInvitation> CreateInvitation(
        Guid blogId, string blogTitle, Guid userId, string username, TokenType type)
    {
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
        return new BlogInvitation
        {
            TokenId = tokenId,
            BlogId = blogId,
            BlogTitle = blogTitle,
            InvitedUser = new GeneralUser { UserId = userId, Username = username },
            InvitedBy = new GeneralUser { UserId = currentUser.UserId, Username = currentUser.Username },
            TargetRole = type == TokenType.BlogAssistantInvitation ? BlogRole.Assistant : BlogRole.Reader,
            CreatedUtc = now,
            ExpiresUtc = InvitationPolicy.ExpiresAt(now)
        };
    }

    /// <inheritdoc />
    public async Task AcceptInvitation(Guid tokenId)
    {
        var invitation = await ReadOwnInvitation(tokenId);

        if (invitation.TargetRole == BlogRole.Assistant)
        {
            // The assistant row and the spent invitation go in together, so the
            // invitation cannot stay live next to somebody who holds what it grants.
            await _repository.AcceptAssistantInvitation(new AddBlogAssistantEntity
            {
                BlogId = invitation.BlogId,
                UserId = _identityProvider.Current.User.UserId,
                JoinedUtc = _dateTimeProvider.Now
            }, tokenId);
        }
        else
        {
            // Past BlogUserService.Subscribe, the public door that refuses a private
            // draft, which is exactly the blog an invitation is issued for. The
            // subscription goes first: it is idempotent, so following the link again
            // repairs a break, while the other order would spend the invitation.
            await _subscriptionService.SubscribeAsync(invitation.BlogId);
            await _repository.Invalidate(tokenId);
        }

        await _eventProducer.SendAsync(EventType.BlogInvitationAccepted, tokenId);
    }

    /// <inheritdoc />
    public async Task RejectInvitation(Guid tokenId)
    {
        await ReadOwnInvitation(tokenId);

        await _repository.Invalidate(tokenId);
        await _eventProducer.SendAsync(EventType.BlogInvitationRejected, tokenId);
    }

    private async Task<BlogInvitation> ReadOwnInvitation(Guid tokenId)
    {
        var invitation = await _repository.GetInvitation(tokenId);

        if (invitation == null || invitation.InvitedUser.UserId != _identityProvider.Current.User.UserId)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        if (invitation.TargetRole != BlogRole.Assistant && invitation.TargetRole != BlogRole.Reader)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        if (_dateTimeProvider.Now > invitation.ExpiresUtc)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.InvitationExpired);
        }

        return invitation;
    }

    /// <inheritdoc />
    public async Task CancelInvitation(Guid tokenId)
    {
        var invitation = await _repository.GetInvitation(tokenId);
        if (invitation == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.InvitationNotFound);
        }

        var blog = await _blogService.GetUnfiltered(invitation.BlogId);
        _intentionManager.ThrowIfForbidden(BlogIntention.CancelInvitation, blog);

        await _repository.Invalidate(tokenId);

        await _eventProducer.SendAsync(EventType.BlogInvitationCancelled, tokenId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetPendingInvitations(Guid blogId)
    {
        // The list feeds the settings page, and assistants invite readers, as on the game side.
        await _intentionManager.GetPermittedBlogAsync(_blogService, BlogIntention.EditSettings, blogId);

        return await _repository.GetPendingInvitations(blogId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetUserPendingInvitations()
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _repository.GetUserPendingInvitations(userId);
    }
}
