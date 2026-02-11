using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Uploading.Authorization;

/// <inheritdoc />
internal class UploadIntentionResolver : IIntentionResolver<UploadIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, UploadIntention intention) => intention switch
    {
        // Listing all uploads - admin only
        UploadIntention.ListAll => user.IsAuthenticated && user.Role >= UserRole.Admin,

        // Listing specific user's uploads - admin only
        UploadIntention.ListUser => user.IsAuthenticated && user.Role >= UserRole.Admin,

        _ => false
    };
}
