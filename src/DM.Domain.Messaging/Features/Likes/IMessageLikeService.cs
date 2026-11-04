using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Likes;

public interface IMessageLikeService
{
    /// <summary>
    /// Like the message as the current user
    /// </summary>
    /// <returns>User who liked the message</returns>
    Task<GeneralUser> LikeMessageAsync(Guid messageId);

    Task UnlikeMessageAsync(Guid messageId);

    /// <summary>
    /// Like a global chat message, reached through the read of the global chat; who may
    /// like is <see cref="Authorization.MessageIntention.Like"/> all the same
    /// </summary>
    /// <returns>User who liked the message</returns>
    Task<GeneralUser> LikeGlobalChatMessageAsync(Guid messageId);

    Task UnlikeGlobalChatMessageAsync(Guid messageId);
}
