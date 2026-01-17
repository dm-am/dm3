using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Messaging.Likes;

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
    Task DislikeMessage(Guid messageId);
}
