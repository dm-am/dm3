using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using System;
using System.Collections.Generic;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Blog.Tests.Authorization;

public class BlogIntentionResolverShould
{
    private readonly BlogIntentionResolver resolver = new();
    private readonly Guid ownerId = Guid.NewGuid();
    private readonly Guid otherUserId = Guid.NewGuid();
    private readonly Guid assistantId = Guid.NewGuid();
    private readonly Guid mentorId = Guid.NewGuid();

    private static AuthenticatedUser CreateUser(
        Guid userId, UserRole role = UserRole.RegularUser, AccessPolicy accessPolicy = AccessPolicy.NotSpecified) =>
        Create.User(userId).WithRole(role).WithAccessPolicy(accessPolicy).Please();

    private BlogDto CreateBlog(
        DraftVisibility draftVisibility = DraftVisibility.Public,
        ModuleStatus status = ModuleStatus.Draft,
        IEnumerable<BlogAssistantInfo>? assistants = null,
        bool viewerIsSubscriber = false,
        GeneralUser? mentor = null,
        IReadOnlySet<Guid>? blacklistedUserIds = null)
    {
        return new BlogDto
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = ownerId },
            Mentor = mentor,
            DraftVisibility = draftVisibility,
            Status = status,
            Assistants = assistants ?? Array.Empty<BlogAssistantInfo>(),
            IsViewerSubscriber = viewerIsSubscriber,
            PendingInvitedUserIds = Array.Empty<Guid>(),
            BlacklistedUserIds = blacklistedUserIds ?? new HashSet<Guid>()
        };
    }

    [Fact]
    public void AllowBlacklistedUserToViewTheBlog()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog(
            draftVisibility: DraftVisibility.Public,
            blacklistedUserIds: new HashSet<Guid> { otherUserId });

        // Same rule as the game side: the blacklist closes writing and not
        // reading. The blog is public to everybody else, so hiding it from one
        // person would promise a privacy it does not have.
        resolver.IsAllowed(user, BlogIntention.ViewDraft, blog).Should().BeTrue();
    }

    // Senior moderation may edit the blog but does not speak for its author, hence an intention of
    // its own.
    [Fact]
    public void AllowSubmitForApprovalToTheBlogOwnerAlone()
    {
        var blog = CreateBlog(
            assistants: new[] { new BlogAssistantInfo { UserId = assistantId } },
            mentor: new GeneralUser { UserId = mentorId });

        resolver.IsAllowed(CreateUser(ownerId), BlogIntention.SubmitForApproval, blog)
            .Should().BeTrue();
        resolver.IsAllowed(CreateUser(assistantId), BlogIntention.SubmitForApproval, blog)
            .Should().BeFalse();
        resolver.IsAllowed(CreateUser(mentorId, UserRole.Mentor), BlogIntention.SubmitForApproval, blog)
            .Should().BeFalse();
        resolver.IsAllowed(CreateUser(otherUserId, UserRole.SeniorModerator), BlogIntention.SubmitForApproval, blog)
            .Should().BeFalse();
        resolver.IsAllowed(CreateUser(Guid.Empty, UserRole.Guest), BlogIntention.SubmitForApproval, blog)
            .Should().BeFalse();
    }

    // The guest's empty id is no identity: it matches no lead even where nobody filled the author
    // or an assistant.
    [Theory]
    [InlineData(ModuleStatus.Draft, BlogIntention.SetStatusActive)]
    [InlineData(ModuleStatus.Closed, BlogIntention.SetStatusActive)]
    [InlineData(ModuleStatus.Active, BlogIntention.SetStatusClosed)]
    [InlineData(ModuleStatus.Closed, BlogIntention.SetStatusClosed)]
    [InlineData(ModuleStatus.Draft, BlogIntention.SubmitForApproval)]
    public void RefuseAGuestTheLeadMoves(ModuleStatus status, BlogIntention intention)
    {
        var blog = CreateBlog(status: status, assistants: new[] { new BlogAssistantInfo() });
        blog.Author = new GeneralUser();

        resolver.IsAllowed(CreateUser(Guid.Empty, UserRole.Guest), intention, blog).Should().BeFalse();
    }

    [Fact]
    public void DenyBlacklistedUserToComment()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog(blacklistedUserIds: new HashSet<Guid> { otherUserId });

        // The other half: reading is open, writing is not
        resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeFalse();
    }

    [Fact]
    public void AllowOwnerToEdit()
    {
        var user = CreateUser(ownerId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.Edit, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonOwnerToEdit()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.Edit, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowAdminToEdit()
    {
        var user = CreateUser(otherUserId, UserRole.Admin);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.Edit, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowOwnerToEditSettings()
    {
        var user = CreateUser(ownerId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAssistantToEditSettings()
    {
        var user = CreateUser(assistantId);
        var blog = CreateBlog(assistants: new[]
        {
            new BlogAssistantInfo { UserId = assistantId, JoinedUtc = DateTimeOffset.UtcNow }
        });

        // The settings page belongs to the blog leads, and assistants are leads
        var result = resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyMentorToEditSettings()
    {
        var user = CreateUser(mentorId, UserRole.Mentor);
        var blog = CreateBlog(mentor: new GeneralUser { UserId = mentorId });

        // Unlike the game curator, the blog mentor only approves publications
        var result = resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyRegularUserToEditSettings()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowSeniorModeratorToEditSettings()
    {
        var user = CreateUser(otherUserId, UserRole.SeniorModerator);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyModeratorToEditSettings()
    {
        var user = CreateUser(otherUserId, UserRole.Moderator);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.EditSettings, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowOwnerToDelete()
    {
        var user = CreateUser(ownerId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.Delete, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowOwnerToCreateRubric()
    {
        var user = CreateUser(ownerId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.ManageRubrics, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonOwnerToCreateRubric()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.ManageRubrics, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowOwnerToCreatePublication()
    {
        var user = CreateUser(ownerId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.CreatePublication, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAssistantToCreatePublication()
    {
        var user = CreateUser(assistantId);
        var blog = CreateBlog(assistants: new[]
        {
            new BlogAssistantInfo { UserId = assistantId, JoinedUtc = DateTimeOffset.UtcNow }
        });

        var result = resolver.IsAllowed(user, BlogIntention.CreatePublication, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonAssistantToCreatePublication()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.CreatePublication, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowAnyoneToViewPublicDraftBlog()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public);

        var result = resolver.IsAllowed(user, BlogIntention.ViewDraft, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonSubscriberToViewPrivateDraftBlog()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private);

        var result = resolver.IsAllowed(user, BlogIntention.ViewDraft, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowSubscriberToViewPrivateDraftBlog()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private, viewerIsSubscriber: true);

        var result = resolver.IsAllowed(user, BlogIntention.ViewDraft, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAdminToViewPrivateDraftBlog()
    {
        var user = CreateUser(otherUserId, UserRole.Admin);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private);

        var result = resolver.IsAllowed(user, BlogIntention.ViewDraft, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowOwnerToInviteAssistant()
    {
        var user = CreateUser(ownerId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.InviteAssistant, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowOwnerToInviteReader()
    {
        var user = CreateUser(ownerId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.InviteReader, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAssistantToInviteReader()
    {
        var user = CreateUser(assistantId);
        var blog = CreateBlog(assistants: new[]
        {
            new BlogAssistantInfo { UserId = assistantId, JoinedUtc = DateTimeOffset.UtcNow }
        });

        var result = resolver.IsAllowed(user, BlogIntention.InviteReader, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyAssistantToInviteAssistant()
    {
        var user = CreateUser(assistantId);
        var blog = CreateBlog(assistants: new[]
        {
            new BlogAssistantInfo { UserId = assistantId, JoinedUtc = DateTimeOffset.UtcNow }
        });

        var result = resolver.IsAllowed(user, BlogIntention.InviteAssistant, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowOwnerToCancelInvitation()
    {
        var user = CreateUser(ownerId);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.CancelInvitation, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowSeniorModeratorToAssignMentor()
    {
        var user = CreateUser(otherUserId, UserRole.SeniorModerator);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.AssignMentor, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyModeratorToAssignMentor()
    {
        var user = CreateUser(otherUserId, UserRole.Moderator);
        var blog = CreateBlog();

        var result = resolver.IsAllowed(user, BlogIntention.AssignMentor, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowAuthenticatedUserToCommentOnPublicBlog()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public);

        var result = resolver.IsAllowed(user, BlogIntention.CreateComment, blog);

        result.Should().BeTrue();
    }

    // The draft visibility closes a blog while it is a draft and nothing after: a blog started from
    // a private draft takes comments from anybody, as it is read by anybody.
    [Fact]
    public void AllowCommentingABlogStartedFromAPrivateDraft()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private, status: ModuleStatus.Active);

        var result = resolver.IsAllowed(user, BlogIntention.CreateComment, blog);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyAStrangerCommentingAPrivateDraft()
    {
        var user = CreateUser(otherUserId);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private, status: ModuleStatus.Draft);

        var result = resolver.IsAllowed(user, BlogIntention.CreateComment, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGuestToComment()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public);

        var result = resolver.IsAllowed(user, BlogIntention.CreateComment, blog);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyCommentingSomebodyElsesBlogUnderTheOrdinaryBan()
    {
        var user = CreateUser(otherUserId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public);

        resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeFalse();
    }

    [Fact]
    public void AllowCommentingOwnBlogUnderTheOrdinaryBan()
    {
        var user = CreateUser(ownerId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public);

        resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeTrue();
    }

    [Fact]
    public void AllowAnAssistantToCommentTheirBlogUnderTheOrdinaryBan()
    {
        var user = CreateUser(assistantId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(
            draftVisibility: DraftVisibility.Public,
            assistants: new[] { new BlogAssistantInfo { UserId = assistantId } });

        resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeTrue();
    }

    [Fact]
    public void AllowTheCuratorToCommentInTheBlogTheyMentorUnderTheOrdinaryBan()
    {
        var user = CreateUser(mentorId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(
            draftVisibility: DraftVisibility.Public,
            mentor: new GeneralUser { UserId = mentorId });

        // Curating is a job in the blog, the same as in a game
        resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeTrue();
    }

    [Fact]
    public void DenyASubscriberToCommentUnderTheOrdinaryBan()
    {
        var user = CreateUser(otherUserId, accessPolicy: AccessPolicy.DemocraticBan);
        var blog = CreateBlog(
            draftVisibility: DraftVisibility.Public,
            viewerIsSubscriber: true);

        // Reading somebody else's blog is not the same as belonging to it
        resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeFalse();
    }

    [Fact]
    public void DenyCommentingOwnBlogUnderAFullBan()
    {
        var user = CreateUser(ownerId, accessPolicy: AccessPolicy.FullBan);
        var blog = CreateBlog(draftVisibility: DraftVisibility.Public);

        resolver.IsAllowed(user, BlogIntention.CreateComment, blog).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.Mentor, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.SeniorModerator, true)]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.RegularUser, false)]
    public void OpenAPremoderatedBlogToExactlyTheRanksThatMayJudgeIt(UserRole role, bool expected)
    {
        var user = CreateUser(otherUserId, role);
        var blog = CreateBlog();

        resolver.IsAllowed(user, BlogIntention.ViewPremoderationPending, blog).Should().Be(expected);

        // The two halves of one right: a reader holding no role in the blog sees
        // it exactly when they may move it along premoderation.
        resolver.IsAllowed(user, BlogIntention.ViewPremoderationPending, blog)
            .Should().Be(new BlogIntentionResolverWithoutTarget()
                .IsAllowed(user, BlogIntention.SetStatusModeration));
    }

    [Fact]
    public void KeepAPremoderatedBlogShutToAGuest()
    {
        var guest = CreateUser(Guid.Empty, UserRole.Guest);

        resolver.IsAllowed(guest, BlogIntention.ViewPremoderationPending, CreateBlog())
            .Should().BeFalse();
    }
}
