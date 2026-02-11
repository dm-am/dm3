using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Blogs.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Blogs;

/// <inheritdoc />
internal class PublicationIntentionResolver : IIntentionResolver<PublicationIntention, Publication>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, PublicationIntention intention, Publication target) =>
        intention switch
        {
            // Author or admin can edit
            PublicationIntention.Edit =>
                user.UserId == target.Author.UserId ||
                user.Role >= UserRole.Admin,

            // Author or admin can delete
            PublicationIntention.Delete =>
                user.UserId == target.Author.UserId ||
                user.Role >= UserRole.Admin,

            // Author can publish
            PublicationIntention.Publish =>
                user.UserId == target.Author.UserId,

            // Unpublished can be viewed by author or admin
            PublicationIntention.ViewDraft =>
                target.IsPublished ||
                user.UserId == target.Author.UserId ||
                user.Role >= UserRole.Admin,

            // Anyone can like published publications, except the author
            PublicationIntention.Like =>
                target.IsPublished &&
                user.UserId != target.Author.UserId,

            // Authenticated users can comment on published publications if comments are enabled
            PublicationIntention.CreateComment =>
                user.IsAuthenticated &&
                target.IsPublished &&
                target.CommentsEnabled,

            _ => false
        };
}
