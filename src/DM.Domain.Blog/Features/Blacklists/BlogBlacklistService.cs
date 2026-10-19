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
    private readonly IEventProducer _eventProducer;

    public BlogBlacklistService(
        IBlogBlacklistRepository repository,
        IBlogService blogService,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IEventProducer eventProducer)
    {
        _repository = repository;
        _blogService = blogService;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> Get(Guid blogId, CancellationToken ct = default)
    {
        await _intentionManager.GetPermittedBlogAsync(_blogService, BlogIntention.Edit, blogId, ct);

        return await _repository.GetBlacklist(blogId, ct);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Add(OperateBlogBlacklistLink dto, CancellationToken ct = default)
    {
        var blog = await _intentionManager.GetPermittedBlogAsync(_blogService, BlogIntention.Edit, dto.BlogId, ct);

        var user = await _userLookupService.GetAsync(dto.Username);

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

        // A subscriber stays subscribed: the list closes comments, and a subscription is reading.
        try
        {
            await _repository.Add(dto.BlogId, user.UserId, currentUserId, ct);
        }
        catch (DuplicateEntityException)
        {
            // The request that lost the race against an identical one gets the
            // answer the check above gives, as on the game's list.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyInBlacklist);
        }

        var cancelledInvitations = await _repository.CancelInvitationsForUser(dto.BlogId, user.UserId, ct);
        foreach (var tokenId in cancelledInvitations)
        {
            await _eventProducer.SendAsync(EventType.BlogInvitationCancelled, tokenId);
        }

        return user;
    }

    /// <inheritdoc />
    public async Task Remove(OperateBlogBlacklistLink dto, CancellationToken ct = default)
    {
        var blog = await _intentionManager.GetPermittedBlogAsync(_blogService, BlogIntention.Edit, dto.BlogId, ct);

        var user = await _userLookupService.GetAsync(dto.Username);

        if (!blog.IsBlacklisted(user.UserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.NotInBlacklist);
        }

        await _repository.Remove(dto.BlogId, user.UserId, ct);
    }
}
