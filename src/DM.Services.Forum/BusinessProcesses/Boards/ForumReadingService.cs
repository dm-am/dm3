using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Extensions;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Forum.BusinessProcesses.Common;

namespace DM.Services.Forum.BusinessProcesses.Boards;

/// <inheritdoc />
internal class ForumReadingService : IForumReadingService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly IForumRepository _forumRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;

    /// <inheritdoc />
    public ForumReadingService(
        IIdentityProvider identityProvider,
        IAccessPolicyConverter accessPolicyConverter,
        IForumRepository forumRepository,
        IUnreadCountersRepository unreadCountersRepository)
    {
        _identityProvider = identityProvider;
        _accessPolicyConverter = accessPolicyConverter;
        _forumRepository = forumRepository;
        _unreadCountersRepository = unreadCountersRepository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Dto.Output.Forum>> GetForaList()
    {
        var fora = await GetFora();
        var identity = _identityProvider.Current;

        // Для анонимных пользователей возвращаем объекты из кэша напрямую
        if (!identity.User.IsAuthenticated)
        {
            return fora;
        }

        // Для авторизованных — копируем, чтобы не загрязнять кэш персональными данными
        var foraCopy = fora.Select(f => new Dto.Output.Forum
        {
            Id = f.Id,
            Title = f.Title,
            Description = f.Description,
            CreateTopicPolicy = f.CreateTopicPolicy,
            ViewPolicy = f.ViewPolicy,
            ModeratorIds = f.ModeratorIds,
            TopicsCount = f.TopicsCount,
            CommentsCount = f.CommentsCount,
            UnreadTopicsCount = 0,
            UnreadCommentsCount = 0,
            LastComment = f.LastComment
        }).ToArray();

        var fillTopicsTask = _unreadCountersRepository.FillParentCounters(foraCopy, identity.User.UserId,
            f => f.Id, f => f.UnreadTopicsCount);
        var fillCommentsTask = _unreadCountersRepository.FillTotalUnreadCounters(foraCopy, identity.User.UserId,
            f => f.Id, f => f.UnreadCommentsCount);
        await Task.WhenAll(fillTopicsTask, fillCommentsTask);

        return foraCopy;
    }

    /// <inheritdoc />
    public async Task<Dto.Output.Forum> GetSingleForum(string forumTitle)
    {
        var forum = await GetForum(forumTitle);
        var identity = _identityProvider.Current;
        if (identity.User.IsAuthenticated)
        {
            var topicsTask = _unreadCountersRepository.SelectByParents(
                identity.User.UserId, UnreadEntryType.Message, forum.Id);
            var commentsTask = _unreadCountersRepository.SelectTotalUnreadByParents(
                identity.User.UserId, UnreadEntryType.Message, forum.Id);
            await Task.WhenAll(topicsTask, commentsTask);

            forum.UnreadTopicsCount = topicsTask.Result[forum.Id];
            forum.UnreadCommentsCount = commentsTask.Result[forum.Id];
        }

        return forum;
    }

    /// <inheritdoc />
    public async Task<Dto.Output.Forum> GetForum(string forumTitle, bool onlyAvailable = true)
    {
        var forum = (await GetFora(onlyAvailable)).FirstOrDefault(f => f.Title == forumTitle);
        if (forum == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Forum {forumTitle} not found");
        }

        return forum;
    }

    private async Task<Dto.Output.Forum[]> GetFora(bool onlyAvailable = true)
    {
        var accessPolicy = onlyAvailable
            ? _accessPolicyConverter.Convert(_identityProvider.Current.User.Role)
            : (ForumAccessPolicy?) null;
        return (await _forumRepository.SelectFora(accessPolicy)).ToArray();
    }
}