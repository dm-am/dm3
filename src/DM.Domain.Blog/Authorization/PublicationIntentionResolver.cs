using DM.Domain.Core.Authorization;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Authorization;

/// <inheritdoc />
internal class PublicationIntentionResolver : IIntentionResolver<PublicationIntention, Publication>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PublicationIntention intention, Publication target)
    {
        var isAuthor = user.UserId == target.Author?.UserId;

        return intention switch
        {
            PublicationIntention.Edit or
            PublicationIntention.Delete =>
                isAuthor || user.Role >= UserRole.Admin,

            // The discussion has no switch that closes it
            PublicationIntention.CreateComment or
            PublicationIntention.Like =>
                user.IsAuthenticated,

            _ => false
        };
    }
}
