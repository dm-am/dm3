using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Shared;

namespace DM.Infrastructure.Persistence.Shared.Likes;

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
