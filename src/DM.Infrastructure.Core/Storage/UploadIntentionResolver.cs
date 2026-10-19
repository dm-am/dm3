using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class UploadIntentionResolver :
    IIntentionResolver<UploadIntention>,
    IIntentionResolver<UploadIntention, StoredUpload>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, UploadIntention intention) => intention switch
    {
        UploadIntention.ListAll or UploadIntention.ListUser => user.IsAuthenticated && user.Role >= UserRole.Moderator,

        // View and Delete need the file: asked here they refuse everybody.
        _ => false
    };

    /// <inheritdoc />
    /// <remarks>
    /// Owner or moderator and above, for looking at a file and for deleting it alike.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, UploadIntention intention, StoredUpload target) =>
        intention switch
        {
            UploadIntention.View or UploadIntention.Delete =>
                user.IsAuthenticated &&
                (target.UserId == user.UserId || user.Role >= UserRole.Moderator),

            _ => false
        };
}
