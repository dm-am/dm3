using System;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Community.Tests.Authorization;

/// <summary>
/// Gates testimonials about the website. They are written on behalf of users by
/// senior moderation, so the name on one is a claim about a real person and
/// rewriting it puts words in their mouth.
/// </summary>
public class WebsiteTestimonialIntentionResolverShould
{
    private readonly WebsiteTestimonialIntentionResolver resolver = new();

    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static WebsiteTestimonial TestimonialBy(Guid authorId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = authorId },
            Text = "Test testimonial"
        };

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void LetSeniorModerationCreateATestimonial(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, WebsiteTestimonialIntention.Create).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void NotLetOrdinaryUsersOrJuniorModerationCreateATestimonial(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        // Testimonials are posted on behalf of users, not by them.
        resolver.IsAllowed(user, WebsiteTestimonialIntention.Create).Should().BeFalse();
    }

    [Fact]
    public void NotLetAGuestCreateATestimonial()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, WebsiteTestimonialIntention.Create).Should().BeFalse();
    }

    [Theory]
    [InlineData(WebsiteTestimonialIntention.Edit)]
    [InlineData(WebsiteTestimonialIntention.Delete)]
    public void RefuseTargetedIntentionsAskedWithoutATestimonial(WebsiteTestimonialIntention intention)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(user, intention).Should().BeFalse();
    }

    [Fact]
    public void LetTheNamedAuthorEditTheirOwnTestimonial()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, WebsiteTestimonialIntention.Edit, TestimonialBy(AuthorId))
            .Should().BeTrue();
    }

    [Fact]
    public void NotLetOneUserRewriteAnothersTestimonial()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Moderator).Please();

        // A testimonial is signed. Junior moderation is below the override, so
        // there is nothing here but being the named author.
        resolver.IsAllowed(user, WebsiteTestimonialIntention.Edit, TestimonialBy(AuthorId))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void LetSeniorModerationEditAnyTestimonial(UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        resolver.IsAllowed(user, WebsiteTestimonialIntention.Edit, TestimonialBy(AuthorId))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void LetSeniorModerationDeleteAnyTestimonial(UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        resolver.IsAllowed(user, WebsiteTestimonialIntention.Delete, TestimonialBy(AuthorId))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void NotLetAnyoneBelowSeniorModerationDeleteATestimonial(UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        resolver.IsAllowed(user, WebsiteTestimonialIntention.Delete, TestimonialBy(AuthorId))
            .Should().BeFalse();
    }

    #region Behaviour as found

    [Fact]
    public void LetTheNamedAuthorEditButNotDeleteTheirOwnTestimonial()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();
        var testimonial = TestimonialBy(AuthorId);

        // Behaviour as found, not a rule that was chosen. Every neighbouring
        // resolver over signed text — UserEndorsement, GameReview, PostReview —
        // lets the author delete what they wrote; this one lets them rewrite it
        // to anything and then keeps it up. Retracting is the weaker act of the
        // two, so requiring more for it is the part that needs a decision.
        resolver.IsAllowed(user, WebsiteTestimonialIntention.Edit, testimonial).Should().BeTrue();
        resolver.IsAllowed(user, WebsiteTestimonialIntention.Delete, testimonial).Should().BeFalse();
    }

    #endregion
}
