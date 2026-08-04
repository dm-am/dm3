using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Likes;

namespace DM.Infrastructure.Persistence.Shared.Likes;

/// <inheritdoc />
internal class LikeOperations : ILikeOperations
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ILikeFactory _likeFactory;
    private readonly ILikeRepository _likeRepository;
    private readonly IEventProducer _producer;

    public LikeOperations(
        IIdentityProvider identityProvider,
        ILikeFactory likeFactory,
        ILikeRepository likeRepository,
        IEventProducer producer)
    {
        _identityProvider = identityProvider;
        _likeFactory = likeFactory;
        _likeRepository = likeRepository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeAsync(ILikable entity, EventType eventType)
    {
        var currentUser = _identityProvider.Current.User;

        // Asked of the store. The entity that was just read carries an empty
        // Likes collection for every aggregate whose mapping profile declares
        // it Ignore - the topic and the blog among them - so this check used to
        // pass on a like that exists, and only the unique index below refused
        // the second one.
        if (await _likeRepository.Exists(entity.Id, currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Conflict,
                RefusalMessage.AlreadyLiked);
        }

        var like = _likeFactory.Create(entity.Id, entity.LikeEntityType, currentUser.UserId);
        try
        {
            await _likeRepository.Add(like);
        }
        catch (DuplicateEntityException)
        {
            // The check above and this one answer the same question; the difference is
            // that the schema answers it after the other request has committed.
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

        // The same question, and the reason this one had to change: All() over
        // an empty collection is true, so on every aggregate that ignores Likes
        // in its mapping this refused unconditionally. A like on a forum topic
        // could be left and never taken back - the heart stayed filled and the
        // site answered "Вы еще не ставили лайк" to the person who had.
        if (!await _likeRepository.Exists(entity.Id, currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Conflict,
                "Вы еще не ставили лайк");
        }

        await _likeRepository.Delete(entity.Id, currentUser.UserId);
    }
}
