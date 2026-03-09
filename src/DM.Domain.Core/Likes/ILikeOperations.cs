using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Likes;

/// <summary>
/// Low-level like operations for domain services
/// </summary>
public interface ILikeOperations
{
    /// <summary>
    /// Like an entity
    /// </summary>
    /// <param name="entity">Likable entity</param>
    /// <param name="eventType">Event type to generate after like</param>
    /// <returns>User who liked</returns>
    Task<GeneralUser> Like(ILikable entity, EventType eventType);

    /// <summary>
    /// Unlike an entity
    /// </summary>
    /// <param name="entity">Likable entity</param>
    Task Unlike(ILikable entity);
}
