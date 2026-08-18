using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using System;
using System.Collections.Generic;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Blog.Tests.Authorization;

public class BlogIntentionResolverShould
{
    private readonly BlogIntentionResolver _resolver = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _assistantId = Guid.NewGuid();
    private readonly Guid _mentorId = Guid.NewGuid();

    private AuthenticatedUser CreateUser(
        Guid userId,
        UserRole role = UserRole.RegularUser,
        AccessPolicy accessPolicy = AccessPolicy.NotSpecified)
    {
        return new AuthenticatedUser
        {
            UserId = userId,
            Role = role,
            AccessPolicy = accessPolicy
        };
    }

    private BlogDto CreateBlog(
        DraftVisibility draftVisibility = DraftVisibility.Public,
        bool commentsEnabled = true,
        IEnumerable<BlogAssistantInfo>? assistants = null,
        bool viewerIsSubscriber = false,
        GeneralUser? mentor = null,
        IReadOnlySet<Guid>? blacklistedUserIds = null)
    {
        return new BlogDto
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = _ownerId },
            Mentor = mentor,
            DraftVisibility = draftVisibility,
            CommentsEnabled = commentsEnabled,
            Assistants = assistants ?? Array.Empty<BlogAssistantInfo>(),
            IsViewerSubscriber = viewerIsSubscriber,
            PendingInvitedUserIds = Array.Empty<Guid>(),
            BlacklistedUserIds = blacklistedUserIds ?? new HashSet<Guid>()
        };
    }

    [Fact]
    public void AllowBlacklistedUserToViewTheBlog()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog(
            draftVisibility: DraftVisibility.Public,
            blacklistedUserIds: new HashSet<Guid> { _otherUserId });

        // Same rule as the game side: the blacklist closes writing and not
        // reading. The blog is public to everybody else, so hiding it from one
        // person would promise a privacy it does not have.
        _resolver.IsAllowed(user, BlogIntention.ViewDraft, blog).Should().BeTrue();
    }

    /// <summary>
    /// Asking for a premoderation verdict is the owner's move and nobody else's.
    /// </summary>
    /// <remarks>
    /// Not the assistant's, who writes in the same blog; not the mentor's, who
    /// approves its publications; not senior moderation's, which may edit the
    /// blog but does not speak for its author. This is why the move has an
    /// intention of its own instead of riding on EditSettings, and it is the same
    /// division the game side draws around its master.
    /// </remarks>
    [Fact]
    public void AllowSubmitForApprovalToTheBlogOwnerAlone()
    {
        var blog = CreateBlog(
            assistants: new[] { new BlogAssistantInfo { UserId = _assistantId } },
            mentor: new GeneralUser { UserId = _mentorId });

        _resolver.IsAllowed(CreateUser(_ownerId), BlogIntention.SubmitForApproval, blog)
            .Should().BeTrue();
        _resolver.IsAllowed(CreateUser(_assistantId), BlogIntention.SubmitForApproval, blog)
            .Should().BeFalse();
        _resolver.IsAllowed(CreateUser(_mentorId, UserRole.Mentor), BlogIntention.SubmitForApproval, blog)
            .Should().BeFalse();
        _resolver.IsAllowed(CreateUser(_otherUserId, UserRole.SeniorModerator), BlogIntention.SubmitForApproval, blog)
            .Should().BeFalse();
        _resolver.IsAllowed(CreateUser(Guid.Empty, UserRole.Guest), BlogIntention.SubmitForApproval, blog)
            .Should().BeFalse();
    }

    [Fact]
    public void DenyBlacklistedUserToComment()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog(blacklistedUserIds: new HashSet<Guid> { _otherUserId });

        // The other half: reading is open, writing is not
        _resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeFalse();
    }

    [Fact]
    public void AllowOwnerToEdit()
    {
        var user = CreateUser(_ownerId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.Edit, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonOwnerToEdit()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.Edit, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowAdminToEdit()
    {
        var user = CreateUser(_otherUserId, UserRole.Admin);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.Edit, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowOwnerToEditSettings()
    {
        var user = CreateUser(_ownerId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAssistantToEditSettings()
    {
        var user = CreateUser(_assistantId);
        var blog = CreateBlog(assistants: new[]
        {
            new BlogAssistantInfo { UserId = _assistantId, JoinedUtc = DateTimeOffset.UtcNow }
        });

        // The settings page belongs to the blog leads, and assistants are leads
        var result = _resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyMentorToEditSettings()
    {
        var user = CreateUser(_mentorId, UserRole.Mentor);
        var blog = CreateBlog(mentor: new GeneralUser { UserId = _mentorId });

        // Unlike the game curator, the blog mentor only approves publications
        var result = _resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyRegularUserToEditSettings()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowSeniorModeratorToEditSettings()
    {
        var user = CreateUser(_otherUserId, UserRole.SeniorModerator);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyModeratorToEditSettings()
    {
        var user = CreateUser(_otherUserId, UserRole.Moderator);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowOwnerToDelete()
    {
        var user = CreateUser(_ownerId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.Delete, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowOwnerToCreateRubric()
    {
        var user = CreateUser(_ownerId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.CreateRubric, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonOwnerToCreateRubric()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.CreateRubric, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowOwnerToCreatePublication()
    {
        var user = CreateUser(_ownerId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.CreatePublication, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAssistantToCreatePublication()
    {
        var user = CreateUser(_assistantId);
        var blog = CreateBlog(assistants: new[]
        {
            new BlogAssistantInfo { UserId = _assistantId, JoinedUtc = DateTimeOffset.UtcNow }
        });

        var result = _resolver.IsAllowed(user, BlogIntention.CreatePublication, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonAssistantToCreatePublication()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.CreatePublication, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowAnyoneToViewPublicDraftBlog()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public);

        var result = _resolver.IsAllowed(user, BlogIntention.ViewDraft, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonSubscriberToViewPrivateDraftBlog()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private);

        var result = _resolver.IsAllowed(user, BlogIntention.ViewDraft, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowSubscriberToViewPrivateDraftBlog()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private, viewerIsSubscriber: true);

        var result = _resolver.IsAllowed(user, BlogIntention.ViewDraft, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAdminToViewPrivateDraftBlog()
    {
        var user = CreateUser(_otherUserId, UserRole.Admin);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private);

        var result = _resolver.IsAllowed(user, BlogIntention.ViewDraft, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowOwnerToInviteAssistant()
    {
        var user = CreateUser(_ownerId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.InviteAssistant, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowOwnerToInviteReader()
    {
        var user = CreateUser(_ownerId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.InviteReader, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAssistantToInviteReader()
    {
        var assistantId = Guid.NewGuid();
        var user = CreateUser(assistantId);
        var blog = CreateBlog(assistants: new[]
        {
            new BlogAssistantInfo { UserId = assistantId, JoinedUtc = DateTimeOffset.UtcNow }
        });

        var result = _resolver.IsAllowed(user, BlogIntention.InviteReader, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyAssistantToInviteAssistant()
    {
        var assistantId = Guid.NewGuid();
        var user = CreateUser(assistantId);
        var blog = CreateBlog(assistants: new[]
        {
            new BlogAssistantInfo { UserId = assistantId, JoinedUtc = DateTimeOffset.UtcNow }
        });

        var result = _resolver.IsAllowed(user, BlogIntention.InviteAssistant, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowOwnerToCancelInvitation()
    {
        var user = CreateUser(_ownerId);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.CancelInvitation, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowMentorToApprovePublications()
    {
        var user = CreateUser(_mentorId);
        var blog = CreateBlog(mentor: new GeneralUser { UserId = _mentorId });

        var result = _resolver.IsAllowed(user, BlogIntention.ApprovePublications, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowSeniorModeratorToAssignMentor()
    {
        var user = CreateUser(_otherUserId, UserRole.SeniorModerator);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.AssignMentor, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyModeratorToAssignMentor()
    {
        var user = CreateUser(_otherUserId, UserRole.Moderator);
        var blog = CreateBlog();

        var result = _resolver.IsAllowed(user, BlogIntention.AssignMentor, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowAuthenticatedUserToCommentOnPublicBlog()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public, commentsEnabled: true);

        var result = _resolver.IsAllowed(user, BlogIntention.CreateComment, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyCommentWhenCommentsDisabled()
    {
        var user = CreateUser(_otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public, commentsEnabled: false);

        var result = _resolver.IsAllowed(user, BlogIntention.CreateComment, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGuestToComment()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public, commentsEnabled: true);

        var result = _resolver.IsAllowed(user, BlogIntention.CreateComment, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyCommentingSomebodyElsesBlogUnderTheOrdinaryBan()
    {
        var user = CreateUser(_otherUserId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public, commentsEnabled: true);

        _resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeFalse();
    }

    [Fact]
    public void AllowCommentingOwnBlogUnderTheOrdinaryBan()
    {
        var user = CreateUser(_ownerId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public, commentsEnabled: true);

        _resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeTrue();
    }

    [Fact]
    public void AllowAnAssistantToCommentTheirBlogUnderTheOrdinaryBan()
    {
        var user = CreateUser(_assistantId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(
            draftVisibility: DraftVisibility.Public,
            commentsEnabled: true,
            assistants: new[] { new BlogAssistantInfo { UserId = _assistantId } });

        _resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeTrue();
    }

    [Fact]
    public void AllowTheCuratorToCommentInTheBlogTheyMentorUnderTheOrdinaryBan()
    {
        var user = CreateUser(_mentorId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(
            draftVisibility: DraftVisibility.Public,
            commentsEnabled: true,
            mentor: new GeneralUser { UserId = _mentorId });

        // Curating is a job in the blog, the same as in a game
        _resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeTrue();
    }

    [Fact]
    public void DenyASubscriberToCommentUnderTheOrdinaryBan()
    {
        var user = CreateUser(_otherUserId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(
            draftVisibility: DraftVisibility.Public,
            commentsEnabled: true,
            viewerIsSubscriber: true);

        // Reading somebody else's blog is not the same as belonging to it
        _resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeFalse();
    }

    [Fact]
    public void DenyCommentingOwnBlogUnderAFullBan()
    {
        var user = CreateUser(_ownerId, accessPolicy: AccessPolicy.FullBan);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public, commentsEnabled: true);

        _resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeFalse();
    }

    /// <summary>
    /// Whoever may pass the verdict may open the blog it is pending on. The gate
    /// used to stop at senior moderation while the verdict itself is a site-wide
    /// Mentor+ move, so a mentor and a moderator were shown a queue every entry of
    /// which answered 403, with two verdict buttons drawn on a page they could not
    /// reach.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Mentor, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.SeniorModerator, true)]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.RegularUser, false)]
    public void OpenAPremoderatedBlogToExactlyTheRanksThatMayJudgeIt(UserRole role, bool expected)
    {
        var user = CreateUser(_otherUserId, role);
        var blog = CreateBlog();

        _resolver.IsAllowed(user, BlogIntention.ViewPremoderationPending, blog).Should().Be(expected);

        // The two halves of one right: a reader holding no role in the blog sees
        // it exactly when they may move it along premoderation.
        _resolver.IsAllowed(user, BlogIntention.ViewPremoderationPending, blog)
            .Should().Be(new BlogIntentionResolverWithoutTarget()
                .IsAllowed(user, BlogIntention.SetStatusModeration));
    }

    [Fact]
    public void KeepAPremoderatedBlogShutToAGuest()
    {
        var guest = CreateUser(Guid.Empty, UserRole.Guest);

        _resolver.IsAllowed(guest, BlogIntention.ViewPremoderationPending, CreateBlog())
            .Should().BeFalse();
    }
}

public class BlogIntentionResolverWithoutTargetShould
{
    private readonly BlogIntentionResolverWithoutTarget _resolver = new();

    private AuthenticatedUser CreateUser(Guid userId, UserRole role = UserRole.RegularUser)
    {
        return new AuthenticatedUser
        {
            UserId = userId,
            Role = role
        };
    }

    [Fact]
    public void AllowAuthenticatedUserToCreateBlog()
    {
        var user = CreateUser(Guid.NewGuid());

        var result = _resolver.IsAllowed(user, BlogIntention.Create);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyGuestToCreateBlog()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);

        var result = _resolver.IsAllowed(user, BlogIntention.Create);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowMentorAndAboveToDeliverAPremoderationVerdict(UserRole role)
    {
        var user = CreateUser(Guid.NewGuid(), role);

        var result = _resolver.IsAllowed(user, BlogIntention.SetStatusModeration);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyRegularUserAPremoderationVerdict()
    {
        var user = CreateUser(Guid.NewGuid());

        // Premoderation is what holds a newbie's blog back until somebody
        // experienced has looked at it. A user who could approve their own blog
        // would be waving themselves through - which is exactly why the move an
        // author does have (SubmitForApproval) only asks for a verdict.
        var result = _resolver.IsAllowed(user, BlogIntention.SetStatusModeration);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGuestAPremoderationVerdict()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);

        var result = _resolver.IsAllowed(user, BlogIntention.SetStatusModeration);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(BlogIntention.Edit)]
    [InlineData(BlogIntention.EditSettings)]
    [InlineData(BlogIntention.Delete)]
    [InlineData(BlogIntention.ViewDraft)]
    [InlineData(BlogIntention.CreatePublication)]
    [InlineData(BlogIntention.ApprovePublications)]
    [InlineData(BlogIntention.SetStatusActive)]
    [InlineData(BlogIntention.SetStatusClosed)]
    [InlineData(BlogIntention.SubmitForApproval)]
    public void DenyIntentionsThatNeedABlogWhenAskedWithoutOne(BlogIntention intention)
    {
        var user = CreateUser(Guid.NewGuid(), UserRole.Admin);

        // Every remaining arm of BlogIntention is a question about one blog. Asked
        // without a blog they must fall through, not answer from the role alone.
        var result = _resolver.IsAllowed(user, intention);

        result.Should().BeFalse();
    }
}
