using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Rooms.Reading;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Posts.Reading;

/// <inheritdoc />
internal class PostReadingService : IPostReadingService
{
    private readonly IRoomReadingService _roomReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IPostReadingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public PostReadingService(
        IRoomReadingService roomReadingService,
        IIntentionManager intentionManager,
        IPostReadingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider)
    {
        _roomReadingService = roomReadingService;
        _intentionManager = intentionManager;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Post> posts, PagingResult paging)> Get(Guid roomId, PagingQuery query)
    {
        var room = await _roomReadingService.Get(roomId);
        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePost, room);

        var identity = _identityProvider.Current;
        var totalCount = await _repository.Count(roomId, identity.User.UserId);
        var paging = new PagingData(query, identity.Settings.Paging.PostsPerPage, totalCount);

        var posts = await _repository.Get(roomId, paging, identity.User.UserId);

        return (posts, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Post> Get(Guid postId)
    {
        var post = await _repository.Get(postId, _identityProvider.Current.User.UserId);
        if (post == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Post not found");
        }

        return post;
    }

    /// <inheritdoc />
    public async Task MarkAsRead(Guid roomId)
    {
        await _roomReadingService.Get(roomId);
        await _unreadCountersRepository.Flush(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, roomId);
    }

    /// <inheritdoc />
    public Task<BestPostResult?> GetBestPost(Guid userId)
    {
        return _repository.GetBestPost(userId);
    }
}