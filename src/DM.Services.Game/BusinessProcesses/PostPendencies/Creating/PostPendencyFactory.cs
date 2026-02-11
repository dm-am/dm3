using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Creating;

/// <inheritdoc />
internal class PostPendencyFactory : IPostPendencyFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PostPendencyFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public DataAccess.BusinessObjects.Games.Links.PostPendency Create(CreatePostPendency createPostPendency, Guid createdById, Guid waitingForUserId)
    {
        return new DataAccess.BusinessObjects.Games.Links.PostPendency
        {
            PendencyId = _guidFactory.Create(),
            RoomId = createPostPendency.RoomId,
            CharacterId = createPostPendency.CharacterId,
            CreatedById = createdById,
            WaitingForUserId = waitingForUserId,
            CreatedUtc = _dateTimeProvider.Now,
            IsRemoved = false
        };
    }
}
