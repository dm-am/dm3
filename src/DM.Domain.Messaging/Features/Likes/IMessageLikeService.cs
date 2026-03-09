using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Likes;

/// <summary>
/// Service for message likes
/// </summary>
public interface IMessageLikeService
{
    /// <summary>
    /// Create new like from current user to selected message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <returns>User who liked the message</returns>
    Task<GeneralUser> LikeMessage(Guid messageId);

    /// <summary>
    /// Remove existing like from current user to selected message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <returns></returns>
    Task UnlikeMessage(Guid messageId);
}
