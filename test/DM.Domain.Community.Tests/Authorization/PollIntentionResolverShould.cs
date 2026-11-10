using System;
using System.Linq;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Authorization;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Domain.Core.Enums;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Abstractions;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Authorization;

public class PollIntentionResolverShould : UnitTestBase
{
    private readonly PollIntentionResolver resolver;
    private readonly DateTimeOffset now = new(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public PollIntentionResolverShould()
    {
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);
        resolver = new PollIntentionResolver(dateTimeProvider);
    }

    private Poll PollEndingIn(int days, params Guid[] options) => new()
    {
        EndsUtc = now.AddDays(days),
        Options = [.. options.Select(id => new PollOption { Id = id })]
    };

    [Theory]
    [InlineData(PollIntention.Create)]
    [InlineData(PollIntention.Vote)]
    [InlineData(PollIntention.Unvote)]
    [InlineData(PollIntention.Edit)]
    [InlineData(PollIntention.Delete)]
    public void ForbidEverythingForGuest(PollIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.RegularUser, false)]
    [InlineData(UserRole.Mentor, false)]
    [InlineData(UserRole.Moderator, false)]
    [InlineData(UserRole.SeniorModerator, true)]
    [InlineData(UserRole.Admin, true)]
    public void LetOnlySeniorModerationCreateEditAndDeleteAPoll(UserRole role, bool allowed)
    {
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, PollIntention.Create).Should().Be(allowed);
        resolver.IsAllowed(user, PollIntention.Edit, new Poll()).Should().Be(allowed);
        resolver.IsAllowed(user, PollIntention.Delete, new Poll()).Should().Be(allowed);
    }

    [Fact]
    public void AllowVoteForAuthenticatedUserOnActivePoll()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        var optionId = Guid.NewGuid();

        resolver.IsAllowed(user, PollIntention.Vote, (PollEndingIn(1, optionId), optionId)).Should().BeTrue();
    }

    [Fact]
    public void ForbidVoteForGuestOnActivePoll()
    {
        var optionId = Guid.NewGuid();

        resolver.IsAllowed(AuthenticatedUser.Guest, PollIntention.Vote, (PollEndingIn(1, optionId), optionId))
            .Should().BeFalse();
    }

    [Fact]
    public void ForbidVoteOnExpiredPoll()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        var optionId = Guid.NewGuid();

        resolver.IsAllowed(user, PollIntention.Vote, (PollEndingIn(-1, optionId), optionId)).Should().BeFalse();
    }

    [Fact]
    public void ForbidVoteWithInvalidOptionId()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PollIntention.Vote, (PollEndingIn(1, Guid.NewGuid()), Guid.NewGuid()))
            .Should().BeFalse();
    }

    [Fact]
    public void AllowUnvoteForAuthenticatedUserOnActivePoll()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PollIntention.Unvote, PollEndingIn(1)).Should().BeTrue();
    }

    [Fact]
    public void ForbidUnvoteOnExpiredPoll()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PollIntention.Unvote, PollEndingIn(-1)).Should().BeFalse();
    }
}
