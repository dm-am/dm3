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

    private AuthenticatedUser CreateUser(Guid userId, UserRole role = UserRole.RegularUser)
    {
        return new AuthenticatedUser
        {
            UserId = userId,
            Role = role
        };
    }

    private BlogModel CreateBlog(
        DraftVisibility draftVisibility = DraftVisibility.Public,
        bool commentsEnabled = true,
        IEnumerable<BlogAssistantInfo>? assistants = null,
        IReadOnlySet<Guid>? subscriberIds = null,
        GeneralUser? mentor = null)
    {
        return new BlogModel
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = _ownerId },
            Mentor = mentor,
            DraftVisibility = draftVisibility,
            CommentsEnabled = commentsEnabled,
            Assistants = assistants ?? Array.Empty<BlogAssistantInfo>(),
            SubscriberIds = subscriberIds ?? new HashSet<Guid>(),
            PendingInvitedUserIds = Array.Empty<Guid>()
        };
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
        var blog = CreateBlog(draftVisibility: DraftVisibility.Private, subscriberIds: new HashSet<Guid> { _otherUserId });

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
}
