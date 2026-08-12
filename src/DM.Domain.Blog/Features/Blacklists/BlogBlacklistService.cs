using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Core.Events;
using DM.Domain.Core.Subscriptions;

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
    private readonly IMapper _mapper;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public BlogBlacklistService(
        IBlogBlacklistRepository repository,
        IBlogService blogService,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IEventProducer producer,
        IMapper mapper,
        ISubscriptionRepository subscriptionRepository)
    {
        _repository = repository;
        _blogService = blogService;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _producer = producer;
        _mapper = mapper;
        _subscriptionRepository = subscriptionRepository;
    }

    private async Task<IEnumerable<GeneralUser>> GetBlacklistAsync(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlogAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        return await _repository.GetBlacklist(blogId, ct);
    }

    private async Task<GeneralUser> AddToBlacklistAsync(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlogAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var user = await _userLookupService.GetAsync(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        // Cannot blacklist Owner
        if (user.UserId == blog.Author.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя добавить в черный список автора блога");
        }

        // Cannot blacklist Mentor
        if (blog.Mentor != null && user.UserId == blog.Mentor.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя добавить в черный список наставника блога");
        }

        // Cannot blacklist an assistant (they must be removed first)
        var isAssistant = blog.Assistants.Any(a => a.UserId == user.UserId);
        if (isAssistant)
        {
            throw new HttpException(HttpStatusCode.Conflict, "Сначала уберите пользователя из ассистентов блога");
        }

        // Check if already blacklisted
        if (await _repository.IsBlocked(blogId, user.UserId, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Пользователь {username} уже в черном списке");
        }

        var currentUserId = _identityProvider.Current.User.UserId;

        // Add to blacklist
        await _repository.Add(blogId, user.UserId, currentUserId, ct);

        // The game keeps the same invariant by refusing to blacklist a member at
        // all and making the owner remove them first. A blog has no command for
        // removing a reader, the blacklist is that command, so the entry ends the
        // subscription itself. Without this the blacklisted user stayed on the
        // list of readers and kept receiving every publication the blog
        // announced, while BlogSubscriptionGuard refused them the subscription
        // they already had.
        await _subscriptionRepository.DeleteAsync(user.UserId, SubscriptionTargetType.Blog, blogId, ct);

        // Cancel pending invitations for this user
        var cancelledInvitations = await _repository.CancelInvitationsForUser(blogId, user.UserId, ct);
        foreach (var tokenId in cancelledInvitations)
        {
            await _producer.SendAsync(EventType.BlogInvitationCancelled, tokenId);
        }

        return _mapper.Map<GeneralUser>(user);
    }

    private async Task RemoveFromBlacklistAsync(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlogAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var user = await _userLookupService.GetAsync(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        if (!await _repository.IsBlocked(blogId, user.UserId, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Пользователя {username} нет в черном списке");
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
