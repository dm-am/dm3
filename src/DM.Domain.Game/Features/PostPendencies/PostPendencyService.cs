using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// Unified service for post pendency CRUD operations
/// </summary>
internal class PostPendencyService : IPostPendencyService
{
    private readonly IValidator<CreatePostPendency> _validator;
    private readonly IRoomService _roomService;
    private readonly IIntentionManager _intentionManager;
    private readonly IPostPendencyFactory _factory;
    private readonly IUserLookupService _userLookupService;
    private readonly IPostPendencyRepository _repository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    public PostPendencyService(
        IValidator<CreatePostPendency> validator,
        IRoomService roomService,
        IIntentionManager intentionManager,
        IPostPendencyFactory factory,
        IUserLookupService userLookupService,
        IPostPendencyRepository repository,
        IEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _roomService = roomService;
        _intentionManager = intentionManager;
        _factory = factory;
        _userLookupService = userLookupService;
        _repository = repository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    #region Create

    public async Task<PostPendency> CreateAsync(CreatePostPendency createPostPendency)
    {
        await _validator.ValidateAndThrowAsync(createPostPendency);
        var room = await _roomService.GetAsync(createPostPendency.RoomId);
        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePostPendency, room);

        var (_, waitingForUserId) = await _userLookupService.FindUserIdAsync(createPostPendency.WaitingForUsername);
        var currentUserId = _identityProvider.Current.User.UserId;

        if (room.Pendencies.Any(e =>
                e.CreatedBy.UserId == currentUserId &&
                e.WaitingForUser.UserId == waitingForUserId))
        {
            throw new HttpException(HttpStatusCode.Conflict,
                $"Ожидание хода для {createPostPendency.WaitingForUsername} уже есть");
        }

        if (room.Accesses.All(a => a.Character.Author.UserId != waitingForUserId))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(PostPendency.WaitingForUser)] = ValidationError.Invalid
            });
        }

        var pendencyToCreate = _factory.Create(createPostPendency, currentUserId, waitingForUserId);
        var pendency = await _repository.Create(pendencyToCreate);
        await _producer.SendAsync(EventType.RoomPendencyCreated, pendency.Id);

        return pendency;
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid pendencyId)
    {
        var pendency = await _repository.Get(pendencyId);
        if (pendency == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Ожидание хода не найдено");
        }

        _intentionManager.ThrowIfForbidden(RoomIntention.DeletePostPendency, pendency);
        await _repository.Delete(pendencyId);
        await _producer.SendAsync(EventType.RoomPendencyDeleted, pendencyId);
    }

    #endregion
}
