using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Blogs;
using BlogDto = DM.Services.Community.BusinessProcesses.Blogs.Reading.Blog;

namespace DM.Services.Community.BusinessProcesses.Blogs;

/// <inheritdoc />
internal class BlogIntentionResolver : IIntentionResolver<BlogIntention, BlogDto>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, BlogIntention intention, BlogDto target)
    {
        var isOwner = user.UserId == target.Owner.UserId;
        var isMentor = target.Mentor?.UserId == user.UserId ||
                       target.Participants.Any(p => p.UserId == user.UserId && p.Role == BlogParticipation.Mentor);
        var isAssistant = target.Participants.Any(p => p.UserId == user.UserId && p.Role == BlogParticipation.Assistant);
        var isParticipant = target.Participants.Any(p => p.UserId == user.UserId);

        return intention switch
        {
            // Owner or admin can edit
            BlogIntention.Edit =>
                isOwner || user.Role >= UserRole.Admin,

            // Owner or admin can delete
            BlogIntention.Delete =>
                isOwner || user.Role >= UserRole.Admin,

            // Owner can create rubrics
            BlogIntention.CreateRubric =>
                isOwner,

            // Owner and assistants can create publications
            BlogIntention.CreatePublication =>
                isOwner || isAssistant,

            // Private blogs can be viewed by owner, participants, mentors, admins
            BlogIntention.ViewPrivate =>
                target.IsPublic ||
                isOwner ||
                isParticipant ||
                isMentor ||
                user.Role >= UserRole.Admin,

            // Owner can manage participants
            BlogIntention.ManageParticipants =>
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

            _ => false
        };
    }
}

/// <inheritdoc />
internal class BlogIntentionResolverWithoutTarget : IIntentionResolver<BlogIntention>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, BlogIntention intention) =>
        intention switch
        {
            // Any authenticated user can create a blog
            BlogIntention.Create => user.IsAuthenticated,

            _ => false
        };
}
