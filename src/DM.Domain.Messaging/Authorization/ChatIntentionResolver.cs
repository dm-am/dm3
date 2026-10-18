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
            // (active event restrictions are checked separately in MessageCreatingService).
            // The global chat is public speech, so an ordinary ban silences it.
            ChatIntention.CreateMessage when target.Type == ChatType.Global =>
                user.IsAuthenticated && user.MaySpeak(),

            // Direct/Group: only participants can send messages. Private
            // correspondence is deliberately outside the ordinary ban.
            ChatIntention.CreateMessage =>
                target.Participants?.Any(p => p.UserId == user.UserId) ?? false,

            // Only group chats can be updated, by participants
            ChatIntention.UpdateChat =>
                target.Type == ChatType.Group &&
                (target.Participants?.Any(p => p.UserId == user.UserId) ?? false),

            // Only group chats can be deleted, by participants
            ChatIntention.DeleteChat =>
                target.Type == ChatType.Group &&
                (target.Participants?.Any(p => p.UserId == user.UserId) ?? false),

            // A participant leaves a group. A direct chat has nobody to stay in it
            // after one of the two is gone, so it is not left.
            ChatIntention.LeaveChat =>
                target.Type == ChatType.Group &&
                (target.Participants?.Any(p => p.UserId == user.UserId) ?? false),

            // A participant clears his own view of a direct or group chat. The
            // global chat and a game room chat have no participant rows to keep
            // the mark on.
            ChatIntention.ClearHistory =>
                target.Type is ChatType.Direct or ChatType.Group &&
                (target.Participants?.Any(p => p.UserId == user.UserId) ?? false),

            _ => false
        };
}
