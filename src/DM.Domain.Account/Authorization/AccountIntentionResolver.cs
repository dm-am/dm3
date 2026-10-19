using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Authorization;

/// <summary>
/// Resolver for account management intentions.
/// All account operations but one are self-targeted (user operates on their own account).
/// </summary>
internal class AccountIntentionResolver : IIntentionResolver<AccountIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, AccountIntention intention) =>
        intention switch
        {
            AccountIntention.ChangePassword or AccountIntention.ChangeEmail or AccountIntention.Deactivate or
                AccountIntention.ViewSessions or AccountIntention.TerminateSession => user.IsAuthenticated,

            // Renames somebody else back, so it belongs to the rank that reviews
            // the requests in the first place
            AccountIntention.RollbackUsernameChange => user.Role >= UserRole.SeniorModerator,

            _ => false
        };
}
