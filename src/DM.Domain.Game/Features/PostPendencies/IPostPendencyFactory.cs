using System;

namespace DM.Domain.Game.Features.PostPendencies;

internal interface IPostPendencyFactory
{
    CreatePostPendencyEntity Create(CreatePostPendency createPostPendency, Guid createdById, Guid waitingForUserId);
}
