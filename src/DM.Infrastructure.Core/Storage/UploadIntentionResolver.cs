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
        // Listing all uploads across users - moderator and above (doc 4.2.3.8.9:
        // the "Все загруженные файлы" page is available to moderator/senior mod/admin)
        UploadIntention.ListAll => user.IsAuthenticated && user.Role >= UserRole.Moderator,

        // Listing specific user's uploads - moderator and above
        UploadIntention.ListUser => user.IsAuthenticated && user.Role >= UserRole.Moderator,

        // View and Delete are questions about one file and cannot be answered
        // without it: asked here they refuse everybody, the owner included.
        _ => false
    };

    /// <inheritdoc />
    /// <remarks>
    /// Owner or moderator+, for looking at a file and for deleting it alike. The
    /// rule the two intentions name was written out at the call sites instead -
    /// the same two lines twice - while the intentions themselves resolved to
    /// nothing, so asking them would have refused the owner of the file.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, UploadIntention intention, StoredUpload target) =>
        intention switch
        {
            UploadIntention.View or UploadIntention.Delete =>
                user.IsAuthenticated &&
                (target.UserId == user.UserId || user.Role >= UserRole.Moderator),

            // Listing is not about one file; the target-less arm answers it.
            _ => false
        };
}
