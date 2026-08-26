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
    Task<GeneralUser> LikeMessageAsync(Guid messageId);

    /// <summary>
    /// Remove existing like from current user to selected message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    Task UnlikeMessageAsync(Guid messageId);

    /// <summary>
    /// Create new like from current user to selected global chat message
    /// </summary>
    /// <remarks>
    /// Reaches the line through the read of the global chat rather than through
    /// the read of a conversation one takes part in; who may leave the like is
    /// <see cref="Authorization.MessageIntention.Like"/>, unchanged — anybody
    /// signed in, except on their own line.
    /// </remarks>
    /// <param name="messageId">Message identifier</param>
    /// <returns>User who liked the message</returns>
    Task<GeneralUser> LikeGlobalChatMessageAsync(Guid messageId);

    /// <summary>
    /// Remove existing like from current user to selected global chat message
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    Task UnlikeGlobalChatMessageAsync(Guid messageId);
}
