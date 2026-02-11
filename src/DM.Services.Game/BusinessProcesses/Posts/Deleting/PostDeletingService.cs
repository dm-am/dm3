using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Posts.Reading;
using DM.Services.Game.BusinessProcesses.Posts.Updating;
using DM.Services.MessageQueuing.GeneralBus;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.Posts.Deleting;

/// <inheritdoc />
internal class PostDeletingService : IPostDeletingService
{
    private readonly IPostReadingService _postReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IPostUpdatingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _producer;
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public PostDeletingService(
        IPostReadingService postReadingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IPostUpdatingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer producer,
        DmDbContext dbContext)
    {
        _postReadingService = postReadingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Delete(Guid postId)
    {
        var post = await _postReadingService.Get(postId);
        _intentionManager.ThrowIfForbidden(PostIntention.Delete, post);
        var updateBuilder = _updateBuilderFactory.Create<Post>(postId)
            .Field(p => p.IsRemoved, true);

        await _repository.Update(updateBuilder);

        // Decrement author's post count (QuantityRating)
        await _dbContext.Users
            .Where(u => u.UserId == post.Author.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, x => x.QuantityRating - 1));

        await _unreadCountersRepository.Decrement(post.RoomId, UnreadEntryType.Message, post.CreatedUtc);
        await _producer.Send(EventType.DeletedPost, postId);
    }
}