using System;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Core.Enums;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Blog.Tests.Authorization;

public class BlogIntentionResolverWithoutTargetShould
{
    private readonly BlogIntentionResolverWithoutTarget resolver = new();

    private static AuthenticatedUser CreateUser(Guid userId, UserRole role = UserRole.RegularUser) =>
        Create.User(userId).WithRole(role).Please();

    [Fact]
    public void AllowAuthenticatedUserToCreateBlog()
    {
        var user = CreateUser(Guid.NewGuid());

        var result = resolver.IsAllowed(user, BlogIntention.Create);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyGuestToCreateBlog()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);

        var result = resolver.IsAllowed(user, BlogIntention.Create);

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

        var result = resolver.IsAllowed(user, BlogIntention.SetStatusModeration);

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
        var result = resolver.IsAllowed(user, BlogIntention.SetStatusModeration);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGuestAPremoderationVerdict()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);

        var result = resolver.IsAllowed(user, BlogIntention.SetStatusModeration);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(BlogIntention.Edit)]
    [InlineData(BlogIntention.EditSettings)]
    [InlineData(BlogIntention.Delete)]
    [InlineData(BlogIntention.ViewDraft)]
    [InlineData(BlogIntention.CreatePublication)]
    [InlineData(BlogIntention.SetStatusActive)]
    [InlineData(BlogIntention.SetStatusClosed)]
    [InlineData(BlogIntention.SubmitForApproval)]
    public void DenyIntentionsThatNeedABlogWhenAskedWithoutOne(BlogIntention intention)
    {
        var user = CreateUser(Guid.NewGuid(), UserRole.Admin);

        // Every remaining arm of BlogIntention is a question about one blog. Asked
        // without a blog they must fall through, not answer from the role alone.
        var result = resolver.IsAllowed(user, intention);

        result.Should().BeFalse();
    }
}
