using System;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Game.Features.PostPendencies;

/// <inheritdoc />
internal class PostPendencyFactory : IPostPendencyFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PostPendencyFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    public CreatePostPendencyEntity Create(CreatePostPendency createPostPendency, Guid createdById, Guid waitingForUserId)
    {
        return new CreatePostPendencyEntity
        {
            PendencyId = _guidFactory.Create(),
            RoomId = createPostPendency.RoomId,
            CharacterId = createPostPendency.CharacterId,
            CreatedById = createdById,
            WaitingForUserId = waitingForUserId,
            CreatedUtc = _dateTimeProvider.Now
        };
    }
}
