using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Core.Events;

namespace DM.Domain.Blog.Features.Blacklists;

/// <inheritdoc />
internal class BlogBlacklistService : IBlogBlacklistService
{
    private readonly IBlogBlacklistRepository _repository;
    private readonly IBlogService _blogService;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IEventProducer _producer;

    public BlogBlacklistService(
        IBlogBlacklistRepository repository,
        IBlogService blogService,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IEventProducer producer)
    {
        _repository = repository;
        _blogService = blogService;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _producer = producer;
    }

    private async Task<IEnumerable<GeneralUser>> GetBlacklistAsync(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlogAsync(blogId, ct);
        await _intentionManager.ThrowIfForbiddenOrHiddenAsync(_blogService, BlogIntention.Edit, blog, ct);

        return await _repository.GetBlacklist(blogId, ct);
    }

    private async Task<GeneralUser> AddToBlacklistAsync(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlogAsync(blogId, ct);
        await _intentionManager.ThrowIfForbiddenOrHiddenAsync(_blogService, BlogIntention.Edit, blog, ct);

        var user = await _userLookupService.GetAsync(username);

        // The same order and the same words as the game's list: already on it,
        // one of the people who run the blog, somebody who has to leave it first.
        if (blog.IsBlacklisted(user.UserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyInBlacklist);
        }

        if (user.UserId == blog.Author?.UserId || user.UserId == blog.Mentor?.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.CannotBlacklistBlogLead);
        }

        if (blog.Assistants.Any(a => a.UserId == user.UserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.RemoveFromBlogAssistantsFirst);
        }

        var currentUserId = _identityProvider.Current.User.UserId;

        // A subscriber stays subscribed. The list closes comments, and a
        // subscription is reading: the publications it announces are open to the
        // person anyway. The entry used to end the subscription, which is
        // the one thing the list is not for.
        try
        {
            await _repository.Add(blogId, user.UserId, currentUserId, ct);
        }
        catch (DuplicateEntityException)
        {
            // The request that lost the race against an identical one gets the
            // answer the check above gives, as on the game's list: the person is
            // on the list either way.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyInBlacklist);
        }

        // Cancel pending invitations for this user
        var cancelledInvitations = await _repository.CancelInvitationsForUser(blogId, user.UserId, ct);
        foreach (var tokenId in cancelledInvitations)
        {
            await _producer.SendAsync(EventType.BlogInvitationCancelled, tokenId);
        }

        // The lookup already answers the domain shape; the AutoMapper call
        // here was an identity pass-through (same source and target type).
        return user;
    }

    private async Task RemoveFromBlacklistAsync(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlogAsync(blogId, ct);
        await _intentionManager.ThrowIfForbiddenOrHiddenAsync(_blogService, BlogIntention.Edit, blog, ct);

        var user = await _userLookupService.GetAsync(username);

        if (!blog.IsBlacklisted(user.UserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.NotInBlacklist);
        }

        await _repository.Remove(blogId, user.UserId, ct);
    }

    #region IBlogBlacklistService domain-specific methods

    /// <inheritdoc />
    public Task<IEnumerable<GeneralUser>> Get(Guid blogId, CancellationToken ct = default) =>
        GetBlacklistAsync(blogId, ct);

    /// <inheritdoc />
    public Task<GeneralUser> Add(OperateBlogBlacklistLink dto, CancellationToken ct = default) =>
        AddToBlacklistAsync(dto.BlogId, dto.Username, ct);

    /// <inheritdoc />
    public Task Remove(OperateBlogBlacklistLink dto, CancellationToken ct = default) =>
        RemoveFromBlacklistAsync(dto.BlogId, dto.Username, ct);

    #endregion
}
