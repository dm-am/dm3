using DM.Domain.Core.Authorization;

namespace DM.Domain.Personal.Authorization;

/// <summary>
/// Resolver for notification management intentions.
/// All notification operations require authentication.
/// </summary>
internal class NotificationIntentionResolver : IIntentionResolver<NotificationIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, NotificationIntention intention) =>
        intention switch
        {
            NotificationIntention.GenerateBotLinkCode => user.IsAuthenticated,
            NotificationIntention.DisconnectBotChannel => user.IsAuthenticated,

            _ => false
        };
}
