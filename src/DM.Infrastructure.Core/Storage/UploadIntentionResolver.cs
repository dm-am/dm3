using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class UploadIntentionResolver : IIntentionResolver<UploadIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, UploadIntention intention) => intention switch
    {
        // Listing all uploads across users - moderator and above (doc 4.2.3.8.9:
        // the "Все загруженное" page is available to moderator/senior mod/admin)
        UploadIntention.ListAll => user.IsAuthenticated && user.Role >= UserRole.Moderator,

        // Listing specific user's uploads - moderator and above
        UploadIntention.ListUser => user.IsAuthenticated && user.Role >= UserRole.Moderator,

        _ => false
    };
}
