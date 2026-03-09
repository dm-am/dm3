using DM.Domain.Core.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Authorization;

/// <inheritdoc />
internal class PublicationIntentionResolver : IIntentionResolver<PublicationIntention, Publication>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PublicationIntention intention, Publication target)
    {
        var isAuthor = user.UserId == target.Author.UserId;

        return intention switch
        {
            // Author or admin can view drafts
            PublicationIntention.ViewDraft =>
                isAuthor || user.Role >= UserRole.Admin,

            // Author or admin can edit
            PublicationIntention.Edit =>
                isAuthor || user.Role >= UserRole.Admin,

            // Author or admin can publish
            PublicationIntention.Publish =>
                isAuthor || user.Role >= UserRole.Admin,

            // Author or admin can delete
            PublicationIntention.Delete =>
                isAuthor || user.Role >= UserRole.Admin,

            // Any authenticated user can comment if publication is published and comments enabled
            PublicationIntention.CreateComment =>
                target.IsPublished &&
                target.CommentsEnabled &&
                user.IsAuthenticated,

            // Any authenticated user can like published publications
            PublicationIntention.Like =>
                target.IsPublished &&
                user.IsAuthenticated,

            _ => false
        };
    }
}
