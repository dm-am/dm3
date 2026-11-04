using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.GlobalChatEvents;

namespace DM.Domain.Messaging.Authorization;

/// <inheritdoc />
internal class GlobalChatEventIntentionResolver :
    IIntentionResolver<GlobalChatEventIntention>,
    IIntentionResolver<GlobalChatEventIntention, GlobalChatEvent>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, GlobalChatEventIntention intention) => intention switch
    {
        GlobalChatEventIntention.Create => user.Role >= UserRole.SeniorModerator,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, GlobalChatEventIntention intention, GlobalChatEvent target)
    {
        var isOrganizer = target.Participants.Any(p => p.User.UserId == user.UserId && p.IsOrganizer);
        var isParticipant = target.Participants.Any(p => p.User.UserId == user.UserId);

        return intention switch
        {
            GlobalChatEventIntention.Update or GlobalChatEventIntention.Delete or
                GlobalChatEventIntention.AddParticipant or GlobalChatEventIntention.RemoveParticipant => isOrganizer,

            GlobalChatEventIntention.Start => isOrganizer && target.Status == GlobalChatEventStatus.Scheduled,
            GlobalChatEventIntention.End => isOrganizer && target.Status == GlobalChatEventStatus.Live,

            GlobalChatEventIntention.Join => user.IsAuthenticated && target.IsOpen && !isParticipant &&
                                             target.Status is GlobalChatEventStatus.Scheduled or GlobalChatEventStatus.Live,

            // The organizer does not leave his own event.
            GlobalChatEventIntention.Leave => isParticipant && !isOrganizer,

            _ => false
        };
    }
}
