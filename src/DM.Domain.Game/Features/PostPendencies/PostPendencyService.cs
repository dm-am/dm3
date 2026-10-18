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
    private readonly IPostPendencyRepository _repository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    public PostPendencyService(
        IValidator<CreatePostPendency> validator,
        IRoomService roomService,
        IIntentionManager intentionManager,
        IPostPendencyFactory factory,
        IPostPendencyRepository repository,
        IEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _roomService = roomService;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    #region Create

    public async Task<PostPendency> CreateAsync(CreatePostPendency createPostPendency)
    {
        await _validator.ValidateAndThrowAsync(createPostPendency);
        // With the game, not without: the rule reads the roles of the game the
        // room belongs to, and asked with the plain projection the check finds no
        // resolver and refuses everybody.
        var room = await _roomService.GetWithGameAsync(createPostPendency.RoomId);
        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePostPendency, room);

        // The one waited for is the player of the character, read from the room:
        // the character has to be let into it, and an NPC, which has no player,
        // cannot be waited on.
        var player = room.Accesses
            .Select(access => access.Character)
            .FirstOrDefault(character => character?.Id == createPostPendency.CharacterId)?
            .Author;
        if (player == null)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(CreatePostPendency.CharacterId)] = ValidationError.Invalid
            });
        }

        var waitingForUserId = player.UserId;
        var currentUserId = _identityProvider.Current.User.UserId;

        if (room.Pendencies.Any(e =>
                e.CreatedBy?.UserId == currentUserId &&
                e.WaitingForUser.UserId == waitingForUserId))
        {
            throw new HttpException(HttpStatusCode.Conflict,
                $"Ожидание хода для {player.Username} уже есть");
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
