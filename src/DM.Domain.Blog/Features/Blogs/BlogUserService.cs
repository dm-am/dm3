using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;

namespace DM.Domain.Blog.Features.Blogs;

/// <inheritdoc />
/// <remarks>
/// Every move here starts by reading the blog through <see cref="IBlogService" />,
/// which keeps the one rule of who may open a blog. The reader's own moves read it
/// through <see cref="IBlogService.GetVisibleAsync" />: on a blog the caller may
/// not open they answer as on one that is not there.
/// </remarks>
internal class BlogUserService : IBlogUserService
{
    private readonly IBlogService _blogService;
    private readonly IBlogRepository _repository;
    private readonly IBlogSubscriptionService _subscriptionService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;

    public BlogUserService(
        IBlogService blogService,
        IBlogRepository repository,
        IBlogSubscriptionService subscriptionService,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager)
    {
        _blogService = blogService;
        _repository = repository;
        _subscriptionService = subscriptionService;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Subscribe(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _blogService.GetVisibleAsync(blogId, ct);
        ThrowIfOwner(blog, "Нельзя подписаться на собственный блог");

        if (blog.Status == ModuleStatus.Draft && blog.DraftVisibility == DraftVisibility.Private)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Этот блог доступен только по приглашению");
        }

        await _subscriptionService.SubscribeAsync(blogId, ct);
        return _identityProvider.Current.User;
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _blogService.GetVisibleAsync(blogId, ct);
        ThrowIfOwner(blog, "Нельзя отписаться от собственного блога");

        await _subscriptionService.UnsubscribeAsync(blogId, ct);
    }

    /// <inheritdoc />
    public async Task Leave(Guid blogId, CancellationToken ct = default)
    {
        var blog = await _blogService.GetVisibleAsync(blogId, ct);
        ThrowIfOwner(blog, "Нельзя покинуть собственный блог");

        var removed = await _repository.RemoveAssistant(blogId, _identityProvider.Current.User.UserId, ct);
        if (!removed)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Вы не ассистент в этом блоге");
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserReference>> GetReaders(Guid blogId, CancellationToken ct = default)
    {
        await _blogService.GetAsync(blogId, ct);
        return await _subscriptionService.GetReadersAsync(blogId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetAssistants(Guid blogId, CancellationToken ct = default)
    {
        await _blogService.GetAsync(blogId, ct);
        return await _repository.GetAssistantsWithJoinDate(blogId, ct);
    }

    /// <inheritdoc />
    public async Task RemoveAssistant(Guid blogId, string username, CancellationToken ct = default)
    {
        var blog = await _blogService.GetUnfiltered(blogId, ct);
        _intentionManager.ThrowIfForbidden(BlogIntention.Edit, blog);

        var removed = await _repository.RemoveAssistantByUsername(blogId, username, ct);
        if (!removed)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Ассистент {username} в этом блоге не найден");
        }
    }

    private void ThrowIfOwner(Blog blog, string refusal)
    {
        if (blog.Author?.UserId == _identityProvider.Current.User.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, refusal);
        }
    }
}
