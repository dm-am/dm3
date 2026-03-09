using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class UploadIntentionResolver : IIntentionResolver<UploadIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, UploadIntention intention) => intention switch
    {
        // Listing all uploads - admin only
        UploadIntention.ListAll => user.IsAuthenticated && user.Role >= UserRole.Admin,

        // Listing specific user's uploads - admin only
        UploadIntention.ListUser => user.IsAuthenticated && user.Role >= UserRole.Admin,

        _ => false
    };
}
