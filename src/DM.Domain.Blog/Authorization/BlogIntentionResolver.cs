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
        // Filled by the list-shaped reads only. Every intention below is decided
        // on a blog that came from a single-blog read, where it is false — so the
        // branches that mention it have never actually fired. Left as it was:
        // making them fire is a change of who may read a private draft and who may
        // comment, not a performance fix.
        var isSubscriber = target.IsViewerSubscriber;
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

            // Status transitions mirror the game lead bucket: only the blog
            // leads (owner + assistants) may move the blog on the status state
            // machine, and only from a compatible current state
            // (Draft -> Active, Closed -> Active reopen)
            BlogIntention.SetStatusActive when user.IsAuthenticated =>
                (target.Status == ModuleStatus.Draft || target.Status == ModuleStatus.Closed) &&
                (isOwner || isAssistant),

            // Active -> Closed (close/freeze/finish), or Closed -> Closed
            // (change reason, e.g. unfreeze a Frozen blog to a plain Close)
            BlogIntention.SetStatusClosed when user.IsAuthenticated =>
                (target.Status == ModuleStatus.Active || target.Status == ModuleStatus.Closed) &&
                (isOwner || isAssistant),

            // Premoderation-pending blogs are hidden like games: only the owner,
            // assistants, the assigned curator, invited users, and senior
            // moderation can see them until they are approved
            BlogIntention.ViewPremoderationPending =>
                isOwner ||
                isAssistant ||
                isMentor ||
                hasPendingInvitation ||
                user.Role >= UserRole.SeniorModerator,

            // Anyone who can view the blog can comment (if comments enabled and not blacklisted)
            // An ordinary ban silences discussion of other people's blogs; the
            // user's own blog stays open. Publications themselves are a
            // different intention and are not affected.
            BlogIntention.CreateComment =>
                target.CommentsEnabled &&
                user.IsAuthenticated &&
                !target.BlacklistedUserIds.Contains(user.UserId) &&
                (target.DraftVisibility == DraftVisibility.Public || isOwner || isAssistant || isSubscriber || isMentor || user.Role >= UserRole.Admin) &&
                user.MaySpeak(inOwnSpace: target.IsOwnBlog(user.UserId)),

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

            // Site-wide Mentor+ gate for premoderation transitions
            // (state checks live in the service)
            BlogIntention.SetStatusModeration =>
                user.IsAuthenticated && user.Role >= UserRole.Mentor,

            _ => false
        };
}
