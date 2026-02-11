using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents;

/// <inheritdoc />
internal class GlobalChatEventIntentionResolver :
    IIntentionResolver<GlobalChatEventIntention>,
    IIntentionResolver<GlobalChatEventIntention, GlobalChatEvent>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, GlobalChatEventIntention intention) => intention switch
    {
        // Only SeniorModerator+ can create events
        GlobalChatEventIntention.Create => user.Role >= UserRole.SeniorModerator,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, GlobalChatEventIntention intention, GlobalChatEvent target)
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
