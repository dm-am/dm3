using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Chats;

namespace DM.Domain.Messaging.Authorization;

/// <inheritdoc />
internal class ChatIntentionResolver : IIntentionResolver<ChatIntention, Chat>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, ChatIntention intention, Chat target) =>
        intention switch
        {
            // Global chat: any authenticated user can send messages
            // (active event restrictions are checked separately in MessageCreatingService)
            ChatIntention.CreateMessage when target.Type == ChatType.Global =>
                user.IsAuthenticated,

            // Direct/Group: only participants can send messages
            ChatIntention.CreateMessage =>
                target.Participants?.Any(p => p.UserId == user.UserId) ?? false,

            // Only group chats can be updated, by participants
            ChatIntention.UpdateChat =>
                target.Type == ChatType.Group &&
                (target.Participants?.Any(p => p.UserId == user.UserId) ?? false),

            _ => false
        };
}
