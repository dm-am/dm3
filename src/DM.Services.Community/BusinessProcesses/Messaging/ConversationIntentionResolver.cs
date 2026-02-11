using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Messaging;

/// <inheritdoc />
internal class ConversationIntentionResolver : IIntentionResolver<ConversationIntention, Conversation>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, ConversationIntention intention, Conversation target) =>
        intention switch
        {
            // Global chat: any authenticated user can send messages
            // (active event restrictions are checked separately in MessageCreatingService)
            ConversationIntention.CreateMessage when target.Type == ConversationType.Global =>
                user.IsAuthenticated,

            // Direct/Group: only participants can send messages
            ConversationIntention.CreateMessage =>
                target.Participants?.Any(p => p.UserId == user.UserId) ?? false,

            // Only group conversations can be updated, by participants
            ConversationIntention.UpdateConversation =>
                target.Type == ConversationType.Group &&
                (target.Participants?.Any(p => p.UserId == user.UserId) ?? false),

            _ => false
        };
}