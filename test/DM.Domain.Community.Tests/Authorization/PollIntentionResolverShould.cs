using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Authorization;
using DM.Testing.Dsl;
using DM.Domain.Core.Enums;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Abstractions;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Community.Tests.Authorization;

public class PollIntentionResolverShould
{
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly PollIntentionResolver _resolver;
    private readonly DateTimeOffset _now = new(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public PollIntentionResolverShould()
    {
        _dateTimeProvider = new Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(p => p.Now).Returns(_now);
        _resolver = new PollIntentionResolver(_dateTimeProvider.Object);
    }

    [Theory]
    [InlineData(PollIntention.Create)]
    [InlineData(PollIntention.Vote)]
    [InlineData(PollIntention.Unvote)]
    [InlineData(PollIntention.Edit)]
    [InlineData(PollIntention.Delete)]
    public void ForbidEverythingForGuest(PollIntention intention)
    {
        _resolver.IsAllowed(AuthenticatedUser.Guest, intention).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void ForbidCreateForNonSeniorModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        _resolver.IsAllowed(user, PollIntention.Create).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowCreateForSeniorModeratorOrHigher(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        _resolver.IsAllowed(user, PollIntention.Create).Should().BeTrue();
    }

    [Fact]
    public void AllowVoteForAuthenticatedUserOnActivePoll()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        var optionId = Guid.NewGuid();
        var poll = new Poll
        {
            EndsUtc = _now.AddDays(1),
            Options = [new PollOption { Id = optionId }]
        };

        _resolver.IsAllowed(user, PollIntention.Vote, (poll, optionId)).Should().BeTrue();
    }

    [Fact]
    public void ForbidVoteForGuestOnActivePoll()
    {
        var optionId = Guid.NewGuid();
        var poll = new Poll
        {
            EndsUtc = _now.AddDays(1),
            Options = [new PollOption { Id = optionId }]
        };

        _resolver.IsAllowed(AuthenticatedUser.Guest, PollIntention.Vote, (poll, optionId)).Should().BeFalse();
    }

    [Fact]
    public void ForbidVoteOnExpiredPoll()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        var optionId = Guid.NewGuid();
        var poll = new Poll
        {
            EndsUtc = _now.AddDays(-1),
            Options = [new PollOption { Id = optionId }]
        };

        _resolver.IsAllowed(user, PollIntention.Vote, (poll, optionId)).Should().BeFalse();
    }

    [Fact]
    public void ForbidVoteWithInvalidOptionId()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        var poll = new Poll
        {
            EndsUtc = _now.AddDays(1),
            Options = [new PollOption { Id = Guid.NewGuid() }]
        };

        _resolver.IsAllowed(user, PollIntention.Vote, (poll, Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public void AllowUnvoteForAuthenticatedUserOnActivePoll()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        var poll = new Poll { EndsUtc = _now.AddDays(1) };

        _resolver.IsAllowed(user, PollIntention.Unvote, poll).Should().BeTrue();
    }

    [Fact]
    public void ForbidUnvoteOnExpiredPoll()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        var poll = new Poll { EndsUtc = _now.AddDays(-1) };

        _resolver.IsAllowed(user, PollIntention.Unvote, poll).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowEditForSeniorModeratorOrHigher(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var poll = new Poll();

        _resolver.IsAllowed(user, PollIntention.Edit, poll).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void ForbidEditForNonSeniorModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var poll = new Poll();

        _resolver.IsAllowed(user, PollIntention.Edit, poll).Should().BeFalse();
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void AllowDeleteForSeniorModeratorOrHigher(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var poll = new Poll();

        _resolver.IsAllowed(user, PollIntention.Delete, poll).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void ForbidDeleteForNonSeniorModerator(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var poll = new Poll();

        _resolver.IsAllowed(user, PollIntention.Delete, poll).Should().BeFalse();
    }
}
