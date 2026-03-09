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
        // Only SeniorModerator+ can create events
        GlobalChatEventIntention.Create => user.Role >= UserRole.SeniorModerator,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, GlobalChatEventIntention intention, GlobalChatEvent target)
    {
        var isOrganizer = target.Participants?.Any(p => p.User.UserId == user.UserId && p.IsOrganizer) ?? false;
        var isParticipant = target.Participants?.Any(p => p.User.UserId == user.UserId) ?? false;

        return intention switch
        {
            // Only organizers can modify events
            GlobalChatEventIntention.Update => isOrganizer,
            GlobalChatEventIntention.Delete => isOrganizer,

            // Only organizers can control event lifecycle
            GlobalChatEventIntention.Start => isOrganizer && target.Status == GlobalChatEventStatus.Scheduled,
            GlobalChatEventIntention.End => isOrganizer && target.Status == GlobalChatEventStatus.Live,

            // Join is only for open events (any authenticated user)
            GlobalChatEventIntention.Join => user.IsAuthenticated && target.IsOpen && !isParticipant &&
                                       (target.Status == GlobalChatEventStatus.Scheduled || target.Status == GlobalChatEventStatus.Live),

            // Leave is for participants who are not organizers
            GlobalChatEventIntention.Leave => isParticipant && !isOrganizer,

            // Only organizers can manage participants
            GlobalChatEventIntention.AddParticipant => isOrganizer,
            GlobalChatEventIntention.RemoveParticipant => isOrganizer,

            _ => false
        };
    }
}
