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
    public async Task<IEnumerable<GeneralUser>> GetBlacklist(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlog(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        return await _repository.GetBlacklist(blogId, ct);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> AddToBlacklist(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlog(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var user = await _userLookupService.Get(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"User '{username}' not found");
        }

        // Cannot blacklist Owner
        if (user.UserId == blog.Author.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot blacklist the blog owner");
        }

        // Cannot blacklist Mentor
        if (blog.Mentor != null && user.UserId == blog.Mentor.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot blacklist the blog mentor");
        }

        // Cannot blacklist an assistant (they must be removed first)
        var isAssistant = blog.Assistants.Any(a => a.UserId == user.UserId);
        if (isAssistant)
        {
            throw new HttpException(HttpStatusCode.Conflict, "Remove user from blog assistants first before blacklisting");
        }

        // Check if already blacklisted
        if (await _repository.IsBlocked(blogId, user.UserId, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, $"User '{username}' is already blacklisted");
        }

        var currentUserId = _identityProvider.Current.User.UserId;

        // Add to blacklist
        await _repository.Add(blogId, user.UserId, currentUserId, ct);

        // Cancel pending invitations for this user
        var cancelledInvitations = await _repository.CancelInvitationsForUser(blogId, user.UserId, ct);
        foreach (var tokenId in cancelledInvitations)
        {
            await _producer.Send(EventType.BlogInvitationCancelled, tokenId);
        }

        return _mapper.Map<GeneralUser>(user);
    }

    /// <inheritdoc />
    public async Task RemoveFromBlacklist(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await _blogService.GetBlog(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var user = await _userLookupService.Get(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"User '{username}' not found");
        }

        if (!await _repository.IsBlocked(blogId, user.UserId, ct))
        {
            throw new HttpException(HttpStatusCode.Conflict, $"User '{username}' is not in the blacklist");
        }

        await _repository.Remove(blogId, user.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsBlocked(Guid blogId, Guid userId, CancellationToken ct = default)
    {
        return await _repository.IsBlocked(blogId, userId, ct);
    }

    #region IContentBlacklistService implementation

    /// <inheritdoc />
    Task<IEnumerable<GeneralUser>> DM.Domain.Core.Blacklists.IContentBlacklistService.GetBlacklist(
        Guid entityId, CancellationToken ct) => GetBlacklist(entityId, ct);

    /// <inheritdoc />
    Task<GeneralUser> DM.Domain.Core.Blacklists.IContentBlacklistService.AddToBlacklist(
        Guid entityId, string username, CancellationToken ct) => AddToBlacklist(entityId, username, ct);

    /// <inheritdoc />
    Task DM.Domain.Core.Blacklists.IContentBlacklistService.RemoveFromBlacklist(
        Guid entityId, string username, CancellationToken ct) => RemoveFromBlacklist(entityId, username, ct);

    #endregion

    #region IBlogBlacklistService domain-specific methods

    /// <inheritdoc />
    public Task<IEnumerable<GeneralUser>> Get(Guid blogId, CancellationToken ct = default) =>
        GetBlacklist(blogId, ct);

    /// <inheritdoc />
    public Task<GeneralUser> Add(Guid blogId, string username, CancellationToken ct = default) =>
        AddToBlacklist(blogId, username, ct);

    /// <inheritdoc />
    public Task Remove(Guid blogId, string username, CancellationToken ct = default) =>
        RemoveFromBlacklist(blogId, username, ct);

    #endregion
}
