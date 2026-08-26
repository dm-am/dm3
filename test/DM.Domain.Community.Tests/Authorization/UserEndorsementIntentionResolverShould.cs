using System;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Community.Tests.Authorization;

/// <summary>
/// Gates endorsements one user writes about another. The endorsement is signed
/// and shown on the endorsed user's profile, so an allow that is too wide lets
/// somebody rewrite a recommendation over another person's name.
/// </summary>
public class UserEndorsementIntentionResolverShould
{
    private readonly UserEndorsementIntentionResolver resolver = new();

    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid EndorsedId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static UserEndorsement EndorsementBy(Guid authorId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = authorId },
            TargetUser = new GeneralUser { UserId = EndorsedId },
            Text = "Test endorsement"
        };

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Admin)]
    public void LetAnyAuthenticatedUserCreateAnEndorsement(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        // Whether the two have actually played together is an async lookup and
        // stays in the service; the resolver only refuses anonymity.
        resolver.IsAllowed(user, UserEndorsementIntention.Create).Should().BeTrue();
    }

    [Fact]
    public void NotLetAGuestCreateAnEndorsement()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, UserEndorsementIntention.Create).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserEndorsementIntention.Edit)]
    [InlineData(UserEndorsementIntention.Delete)]
    public void RefuseTargetedIntentionsAskedWithoutAnEndorsement(UserEndorsementIntention intention)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(user, intention).Should().BeFalse();
    }

    [Fact]
    public void LetTheAuthorEditTheirOwnEndorsement()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, UserEndorsementIntention.Edit, EndorsementBy(AuthorId))
            .Should().BeTrue();
    }

    [Fact]
    public void NotLetTheEndorsedUserEditWhatWasWrittenAboutThem()
    {
        var user = Create.User(EndorsedId).WithRole(UserRole.RegularUser).Please();

        // Being praised does not make the praise yours to reword.
        resolver.IsAllowed(user, UserEndorsementIntention.Edit, EndorsementBy(AuthorId))
            .Should().BeFalse();
    }

    [Fact]
    public void LetTheAuthorDeleteTheirOwnEndorsement()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, UserEndorsementIntention.Delete, EndorsementBy(AuthorId))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void LetSeniorModerationDeleteAnyEndorsement(UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        resolver.IsAllowed(user, UserEndorsementIntention.Delete, EndorsementBy(AuthorId))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void NotLetAnyoneBelowSeniorModerationDeleteSomebodyElsesEndorsement(UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();

        // An endorsement is a stated opinion about a person, and AUTHORIZATION.md
        // keeps it with profile moderation rather than with comments: a moderator
        // stops here.
        resolver.IsAllowed(user, UserEndorsementIntention.Delete, EndorsementBy(AuthorId))
            .Should().BeFalse();
    }

    #region Behaviour as found

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void GiveModerationNoWayToCorrectAnEndorsementShortOfDeletingIt(UserRole role)
    {
        var user = Create.User(StrangerId).WithRole(role).Please();
        var endorsement = EndorsementBy(AuthorId);

        // Behaviour as found, not a rule that was chosen. Delete carries a
        // moderation override and Edit carries none, so a moderator handling an
        // abusive endorsement has only the blunt option. The asymmetry may well
        // be right — an edited endorsement still bears the author's name — but it
        // is not stated anywhere, so it is pinned rather than assumed.
        resolver.IsAllowed(user, UserEndorsementIntention.Edit, endorsement).Should().BeFalse();
        resolver.IsAllowed(user, UserEndorsementIntention.Delete, endorsement).Should().BeTrue();
    }

    #endregion
}
