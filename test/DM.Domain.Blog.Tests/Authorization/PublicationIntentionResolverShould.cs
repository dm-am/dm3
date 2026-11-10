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

// Gates a publication: the leak that matters is another user rewriting or deleting it.
public class PublicationIntentionResolverShould
{
    private readonly PublicationIntentionResolver resolver = new();

    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static Publication PublicationBy(Guid authorId) =>
        new()
        {
            Id = Guid.NewGuid(),
            BlogId = Guid.NewGuid(),
            Author = new GeneralUser { UserId = authorId }
        };

    [Theory]
    [InlineData(PublicationIntention.Edit)]
    [InlineData(PublicationIntention.Delete)]
    public void LetTheAuthorRunTheirOwnPublication(PublicationIntention intention)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, PublicationBy(AuthorId))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(PublicationIntention.Edit)]
    [InlineData(PublicationIntention.Delete)]
    public void KeepAnotherUsersPublicationAwayFromThem(PublicationIntention intention)
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, PublicationBy(AuthorId))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(PublicationIntention.Edit)]
    [InlineData(PublicationIntention.Delete)]
    public void KeepAnotherUsersPublicationAwayFromAGuest(PublicationIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention, PublicationBy(AuthorId))
            .Should().BeFalse();
    }

    [Fact]
    public void LetAnyAuthenticatedUserCommentOnAPublication()
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
    public void LetAnAuthenticatedUserLikeAPublication()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PublicationIntention.Like, PublicationBy(AuthorId)).Should().BeTrue();
    }

    [Fact]
    public void NotLetAGuestLike()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, PublicationIntention.Like, PublicationBy(AuthorId))
            .Should().BeFalse();
    }

    [Fact]
    public void LetTheAuthorLikeTheirOwnPublication()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        // Behaviour as found: CommentIntention.Like refuses self-likes, a publication allows them.
        resolver.IsAllowed(user, PublicationIntention.Like, PublicationBy(AuthorId)).Should().BeTrue();
    }

    [Fact]
    public void GiveTheBlogOwnerNoSayOverAPublicationInTheirOwnBlog()
    {
        var blogOwner = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var publication = PublicationBy(AuthorId);

        // Behaviour as found: the publication carries only a BlogId, so the owner is a passer-by here.
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

        // Behaviour as found: a senior moderator may delete a blog but not one publication in it.
        resolver.IsAllowed(user, PublicationIntention.Delete, PublicationBy(AuthorId))
            .Should().Be(allowed);
    }
}
