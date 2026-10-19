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
            // The global chat is public speech, so an ordinary ban silences it. A closed
            // event is asked separately, by MessageService.
            ChatIntention.CreateMessage when target.Type == ChatType.Global =>
                user.IsAuthenticated && user.MaySpeak(),

            // Private correspondence is deliberately outside the ordinary ban.
            ChatIntention.CreateMessage =>
                target.Participants.Any(p => p.UserId == user.UserId),

            // A direct chat has nobody to stay in it once one of the two is gone.
            ChatIntention.UpdateChat or ChatIntention.DeleteChat or ChatIntention.LeaveChat =>
                target.Type == ChatType.Group &&
                target.Participants.Any(p => p.UserId == user.UserId),

            // The global chat and a game room chat have no participant rows to keep the mark on.
            ChatIntention.ClearHistory =>
                target.Type is ChatType.Direct or ChatType.Group &&
                target.Participants.Any(p => p.UserId == user.UserId),

            _ => false
        };
}
