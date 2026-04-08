using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Domain.Blog.Authorization;

/// <inheritdoc />
internal class BlogIntentionResolver : IIntentionResolver<BlogIntention, BlogDto>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, BlogIntention intention, BlogDto target)
    {
        var isOwner = user.UserId == target.Author.UserId;
        var isMentor = target.Mentor?.UserId == user.UserId;
        var isAssistant = target.Assistants.Any(a => a.UserId == user.UserId);
        var isSubscriber = target.SubscriberIds.Contains(user.UserId);
        var hasPendingInvitation = target.PendingInvitedUserIds.Contains(user.UserId);

        return intention switch
        {
            // Owner or admin can edit
            BlogIntention.Edit =>
                isOwner || user.Role >= UserRole.Admin,

            // Owner or senior moderator+ can delete
            BlogIntention.Delete =>
                isOwner || user.Role >= UserRole.SeniorModerator,

            // Owner can create rubrics
            BlogIntention.CreateRubric =>
                isOwner,

            // Owner and assistants can create publications
            BlogIntention.CreatePublication =>
                isOwner || isAssistant,

            // Draft blogs with private visibility can be viewed by owner, assistants, readers, mentors, admins, or invited users
            BlogIntention.ViewDraft =>
                target.DraftVisibility == DraftVisibility.Public ||
                isOwner ||
                isAssistant ||
                isSubscriber ||
                isMentor ||
                hasPendingInvitation ||
                user.Role >= UserRole.Admin,

            // Owner can invite assistants
            BlogIntention.InviteAssistant =>
                isOwner,

            // Owner or assistant can invite readers
            BlogIntention.InviteReader =>
                isOwner || isAssistant,

            // Owner can cancel invitations (creator check done in service)
            BlogIntention.CancelInvitation =>
                isOwner,

            // Owner can manage blacklist
            BlogIntention.ManageBlacklist =>
                isOwner,

            // Mentors can approve/reject publications
            BlogIntention.ApprovePublications =>
                isMentor || user.Role >= UserRole.SeniorModerator,

            // Senior moderators+ can assign mentors to blogs
            BlogIntention.AssignMentor =>
                user.Role >= UserRole.SeniorModerator,

            // Anyone who can view the blog can comment (if comments enabled and not blacklisted)
            BlogIntention.CreateComment =>
                target.CommentsEnabled &&
                user.IsAuthenticated &&
                !target.BlacklistedUserIds.Contains(user.UserId) &&
                (target.DraftVisibility == DraftVisibility.Public || isOwner || isAssistant || isSubscriber || isMentor || user.Role >= UserRole.Admin),

            _ => false
        };
    }
}

/// <inheritdoc />
internal class BlogIntentionResolverWithoutTarget : IIntentionResolver<BlogIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, BlogIntention intention) =>
        intention switch
        {
            // Any authenticated user can create a blog
            BlogIntention.Create => user.IsAuthenticated,

            _ => false
        };
}
