using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Likes;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Likes;

/// <inheritdoc />
internal class LikeOperations : ILikeOperations
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _producer;

    public LikeOperations(
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IEventProducer producer)
    {
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeAsync(ILikable entity, EventType eventType)
    {
        var currentUser = _identityProvider.Current.User;

        // Asked of the store: the entity just read may carry an empty Likes collection.
        if (await Exists(entity.Id, currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Conflict,
                RefusalMessage.AlreadyLiked);
        }

        var like = new Like
        {
            LikeId = _guidFactory.Create(),
            UserId = currentUser.UserId,
            EntityId = entity.Id,
            EntityType = entity.LikeEntityType
        };
        _dbContext.Likes.Add(like);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            // Two clicks both passed the check above; the schema answers after the other committed.
            _dbContext.ChangeTracker.Clear();
            throw new HttpException(HttpStatusCode.Conflict,
                RefusalMessage.AlreadyLiked);
        }

        await _producer.SendAsync(eventType, like.LikeId);
        return currentUser;
    }

    /// <inheritdoc />
    public async Task UnlikeAsync(ILikable entity)
    {
        var currentUser = _identityProvider.Current.User;

        if (!await Exists(entity.Id, currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.NotLiked);
        }

        // A like is withdrawn by the person who left it.
        var userId = currentUser.UserId;
        var entityId = entity.Id;
        await SoftDelete.MarkWhere(
            _dbContext.Likes.Where(l => l.UserId == userId && l.EntityId == entityId),
            userId, _dateTimeProvider.Now);
    }

    private Task<bool> Exists(Guid entityId, Guid userId) => _dbContext.Likes
        .AnyAsync(l => !l.IsRemoved && l.EntityId == entityId && l.UserId == userId);
}
