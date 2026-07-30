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

    public BlogBlacklistService(
        IBlogBlacklistRepository repository,
        IBlogService blogService,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IEventProducer producer,
        IMapper mapper)
    {
        _repository = repository;
        _blogService = blogService;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _producer = producer;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetBlacklistAsync(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlogAsync(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        return await _repository.GetBlacklist(blogId, ct);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> AddToBlacklistAsync(Guid blogId, string username, CancellationToken ct = default)
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

        // Cancel pending invitations for this user
        var cancelledInvitations = await _repository.CancelInvitationsForUser(blogId, user.UserId, ct);
        foreach (var tokenId in cancelledInvitations)
        {
            await _producer.SendAsync(EventType.BlogInvitationCancelled, tokenId);
        }

        return _mapper.Map<GeneralUser>(user);
    }

    /// <inheritdoc />
    public async Task RemoveFromBlacklistAsync(Guid blogId, string username, CancellationToken ct = default)
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

    /// <inheritdoc />
    public async Task<bool> IsBlockedAsync(Guid blogId, Guid userId, CancellationToken ct = default)
    {
        return await _repository.IsBlocked(blogId, userId, ct);
    }

    #region IContentBlacklistService implementation

    /// <inheritdoc />
    Task<IEnumerable<GeneralUser>> DM.Domain.Core.Blacklists.IContentBlacklistService.GetBlacklistAsync(
        Guid entityId, CancellationToken ct) => GetBlacklistAsync(entityId, ct);

    /// <inheritdoc />
    Task<GeneralUser> DM.Domain.Core.Blacklists.IContentBlacklistService.AddToBlacklistAsync(
        Guid entityId, string username, CancellationToken ct) => AddToBlacklistAsync(entityId, username, ct);

    /// <inheritdoc />
    Task DM.Domain.Core.Blacklists.IContentBlacklistService.RemoveFromBlacklistAsync(
        Guid entityId, string username, CancellationToken ct) => RemoveFromBlacklistAsync(entityId, username, ct);

    /// <inheritdoc />
    Task<bool> DM.Domain.Core.Blacklists.IContentBlacklistService.IsBlockedAsync(
        Guid entityId, Guid userId, CancellationToken ct) => IsBlockedAsync(entityId, userId, ct);

    #endregion

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
