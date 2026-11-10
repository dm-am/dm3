using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Likes;

/// <summary>
/// Low-level like operations for domain services
/// </summary>
public interface ILikeOperations
{
    Task<GeneralUser> LikeAsync(ILikable entity, EventType eventType);

    Task UnlikeAsync(ILikable entity);
}
