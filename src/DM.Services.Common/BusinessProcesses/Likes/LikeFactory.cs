using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Common;

namespace DM.Services.Common.BusinessProcesses.Likes;

/// <inheritdoc />
internal class LikeFactory : ILikeFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public LikeFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public Like Create(Guid entityId, LikeEntityType entityType, Guid userId)
    {
        return new Like
        {
            LikeId = _guidFactory.Create(),
            UserId = userId,
            EntityId = entityId,
            EntityType = entityType
        };
    }
}