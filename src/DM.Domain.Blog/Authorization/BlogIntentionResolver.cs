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
        var isOwner = user.UserId == target.Author?.UserId;
        var isAssistant = target.Assistants.Any(a => a.UserId == user.UserId);
        var isOwnBlog = target.IsOwnBlog(user.UserId);
        var hasPendingInvitation = target.PendingInvitedUserIds.Contains(user.UserId);
        var seesPrivateDraft = target.DraftVisibility == DraftVisibility.Public ||
                               isOwnBlog ||
                               target.IsViewerSubscriber ||
                               user.Role >= UserRole.Admin;

        return intention switch
        {
            BlogIntention.Edit =>
                isOwner || user.Role >= UserRole.Admin,

            // The mentor is absent: unlike the game mentor, the blog mentor does not help shape the blog.
            BlogIntention.EditSettings =>
                isOwner || isAssistant || user.Role >= UserRole.SeniorModerator,

            BlogIntention.Delete =>
                isOwner || user.Role >= UserRole.SeniorModerator,

            BlogIntention.ManageRubrics =>
                isOwner,

            BlogIntention.CreatePublication =>
                isOwner || isAssistant,

            BlogIntention.ViewDraft =>
                seesPrivateDraft || hasPendingInvitation,

            BlogIntention.InviteAssistant =>
                isOwner,

            BlogIntention.InviteReader =>
                isOwner || isAssistant,

            // The creator check is the service's.
            BlogIntention.CancelInvitation =>
                isOwner,

            BlogIntention.AssignMentor =>
                user.Role >= UserRole.SeniorModerator,

            // The guest's empty id is no identity (AnonymousIdentity), so the status moves ask for a signed-in user.
            // Draft -> Active, and the Closed -> Active reopen
            BlogIntention.SetStatusActive when user.IsAuthenticated =>
                (target.Status == ModuleStatus.Draft || target.Status == ModuleStatus.Closed) &&
                (isOwner || isAssistant),

            // Closed -> Closed changes the reason, e.g. a Frozen blog to a plain Close
            BlogIntention.SetStatusClosed when user.IsAuthenticated =>
                (target.Status == ModuleStatus.Active || target.Status == ModuleStatus.Closed) &&
                (isOwner || isAssistant),

            // Identity only: the status the move is legal from is ModulePremoderationPolicy's answer.
            BlogIntention.SubmitForApproval when user.IsAuthenticated =>
                isOwner,

            // The rank the verdict reads, so whoever may judge a blog may open it.
            BlogIntention.ViewPremoderationPending =>
                isOwnBlog ||
                hasPendingInvitation ||
                user.MayJudgePremoderation(),

            // The discussion has no switch that closes it; the services ask ThrowIfBlacklisted first, for the words.
            BlogIntention.CreateComment =>
                user.IsAuthenticated &&
                !target.IsBlacklisted(user.UserId) &&
                (target.Status != ModuleStatus.Draft || seesPrivateDraft) &&
                user.MaySpeak(inOwnSpace: isOwnBlog),

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
            BlogIntention.Create => user.IsAuthenticated,

            // The rank ViewPremoderationPending reads; the state checks live in the service.
            BlogIntention.SetStatusModeration =>
                user.MayJudgePremoderation(),

            _ => false
        };
}
