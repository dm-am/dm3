using DM.Domain.Core.Authorization;

namespace DM.Domain.Account.Authorization;

/// <summary>
/// Resolver for account management intentions.
/// All account operations are self-targeted (user operates on their own account).
/// </summary>
internal class AccountIntentionResolver : IIntentionResolver<AccountIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, AccountIntention intention) =>
        intention switch
        {
            // All account self-management operations require authentication
            AccountIntention.ChangePassword => user.IsAuthenticated,
            AccountIntention.ChangeEmail => user.IsAuthenticated,
            AccountIntention.Deactivate => user.IsAuthenticated,
            AccountIntention.ViewSessions => user.IsAuthenticated,
            AccountIntention.TerminateSession => user.IsAuthenticated,

            _ => false
        };
}
