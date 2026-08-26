using System;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Blog.Tests.Authorization;

/// <summary>
/// Gates a publication: an unpublished draft is private writing, and the leak
/// that matters is another user reading or rewriting it.
/// </summary>
public class PublicationIntentionResolverShould
{
    private readonly PublicationIntentionResolver resolver = new();

    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static Publication PublicationBy(
        Guid authorId,
        bool isPublished = true,
        bool commentsEnabled = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            BlogId = Guid.NewGuid(),
            Author = new GeneralUser { UserId = authorId },
            IsPublished = isPublished,
            CommentsEnabled = commentsEnabled
        };

    #region The draft

    [Theory]
    [InlineData(PublicationIntention.ViewDraft)]
    [InlineData(PublicationIntention.Edit)]
    [InlineData(PublicationIntention.Publish)]
    [InlineData(PublicationIntention.Delete)]
    public void LetTheAuthorRunTheirOwnPublication(PublicationIntention intention)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, PublicationBy(AuthorId, isPublished: false))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(PublicationIntention.ViewDraft)]
    [InlineData(PublicationIntention.Edit)]
    [InlineData(PublicationIntention.Publish)]
    [InlineData(PublicationIntention.Delete)]
    public void KeepAnotherUsersDraftAwayFromThem(PublicationIntention intention)
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, PublicationBy(AuthorId, isPublished: false))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(PublicationIntention.ViewDraft)]
    [InlineData(PublicationIntention.Edit)]
    [InlineData(PublicationIntention.Publish)]
    [InlineData(PublicationIntention.Delete)]
    public void KeepAnotherUsersDraftAwayFromAGuest(PublicationIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention, PublicationBy(AuthorId, isPublished: false))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    public void NotOpenADraftToModerationBelowAdmin(UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        // Moderation reads what was published. An unpublished draft stays with
        // its author until an admin has a reason to reach for it.
        resolver.IsAllowed(user, PublicationIntention.ViewDraft, PublicationBy(AuthorId, isPublished: false))
            .Should().BeFalse();
    }

    [Fact]
    public void OpenADraftToAnAdmin()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(user, PublicationIntention.ViewDraft, PublicationBy(AuthorId, isPublished: false))
            .Should().BeTrue();
    }

    #endregion

    #region Comments and likes

    [Fact]
    public void LetAnyAuthenticatedUserCommentOnAPublishedPublication()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PublicationIntention.CreateComment, PublicationBy(AuthorId))
            .Should().BeTrue();
    }

    [Fact]
    public void NotLetAGuestComment()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, PublicationIntention.CreateComment, PublicationBy(AuthorId))
            .Should().BeFalse();
    }

    [Fact]
    public void NotLetAnyoneCommentOnAnUnpublishedPublication()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        // Not even its author: there is nothing public to answer yet.
        resolver.IsAllowed(user, PublicationIntention.CreateComment, PublicationBy(AuthorId, isPublished: false))
            .Should().BeFalse();
    }

    [Fact]
    public void HonourCommentsBeingSwitchedOff()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var publication = PublicationBy(AuthorId, commentsEnabled: false);

        resolver.IsAllowed(user, PublicationIntention.CreateComment, publication).Should().BeFalse();
    }

    [Fact]
    public void NotLetAnAdminCommentPastASwitchedOffThread()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();
        var publication = PublicationBy(AuthorId, commentsEnabled: false);

        // The author's switch is not a permission level, so no rank overrides it.
        resolver.IsAllowed(user, PublicationIntention.CreateComment, publication).Should().BeFalse();
    }

    [Fact]
    public void LetAnAuthenticatedUserLikeAPublishedPublication()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PublicationIntention.Like, PublicationBy(AuthorId)).Should().BeTrue();
    }

    [Fact]
    public void NotLetAnyoneLikeAnUnpublishedPublication()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PublicationIntention.Like, PublicationBy(AuthorId, isPublished: false))
            .Should().BeFalse();
    }

    [Fact]
    public void NotLetAGuestLike()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, PublicationIntention.Like, PublicationBy(AuthorId))
            .Should().BeFalse();
    }

    [Fact]
    public void IgnoreTheCommentSwitchWhenLiking()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var publication = PublicationBy(AuthorId, commentsEnabled: false);

        // Closing the thread silences replies, not approval.
        resolver.IsAllowed(user, PublicationIntention.Like, publication).Should().BeTrue();
    }

    #endregion

    #region Behaviour as found

    [Fact]
    public void LetTheAuthorLikeTheirOwnPublication()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        // Behaviour as found, not a rule that was chosen. CommentIntention.Like
        // refuses self-likes outright and has a test saying so; the same action on
        // a publication is allowed. One of the two is wrong, and which one is a
        // product decision.
        resolver.IsAllowed(user, PublicationIntention.Like, PublicationBy(AuthorId)).Should().BeTrue();
    }

    [Fact]
    public void GiveTheBlogOwnerNoSayOverAPublicationInTheirOwnBlog()
    {
        var blogOwner = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var publication = PublicationBy(AuthorId, isPublished: false);

        // Behaviour as found, not a rule that was chosen. The publication carries
        // a BlogId and nothing else about the blog, so the resolver cannot tell
        // the blog's owner from a passer-by. An assistant may publish into a blog
        // (BlogIntention.CreatePublication) and the owner can then neither edit
        // nor delete what appeared there. PublicationCommentService loads the blog
        // itself for exactly this reason; the write paths do not.
        resolver.IsAllowed(blogOwner, PublicationIntention.Edit, publication).Should().BeFalse();
        resolver.IsAllowed(blogOwner, PublicationIntention.Delete, publication).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator, false)]
    [InlineData(UserRole.Admin, true)]
    public void RequireAdminToDeleteAPublicationWhileABlogNeedsOnlySeniorModerator(
        UserRole role, bool allowed)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        // Behaviour as found, not a rule that was chosen. A senior moderator may
        // delete an entire blog (BlogIntention.Delete) but not one publication
        // inside it, so the smaller act needs the higher rank.
        resolver.IsAllowed(user, PublicationIntention.Delete, PublicationBy(AuthorId))
            .Should().Be(allowed);
    }

    #endregion
}
