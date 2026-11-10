using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Tokens;

public class AccountRetentionShould : UnitTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IDateTimeProvider clock;

    public AccountRetentionShould()
    {
        clock = Mock<IDateTimeProvider>();
        clock.Now.Returns(Now);
    }

    [Fact]
    public async Task DropATokenOnceTheRetentionWindowHasPassed()
    {
        var repository = Mock<ITokenMaintenanceRepository>();
        repository
            .DeleteWithdrawnOrIssuedBefore(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(3);

        var deleted = await new TokenCleanupProcessor(repository, clock).DeleteStaleAsync();

        deleted.Should().Be(3);
        await repository.Received(1).DeleteWithdrawnOrIssuedBefore(
            Now.AddDays(-7), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReleaseAnEmailAddressOnceTheRegistrationWindowHasPassed()
    {
        var repository = Mock<IRegistrationRepository>();
        repository
            .DeletePendingStartedBefore(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(2);

        var deleted = await new PendingRegistrationCleanupProcessor(repository, clock)
            .DeleteExpiredAsync();

        deleted.Should().Be(2);
        await repository.Received(1).DeletePendingStartedBefore(
            Now.AddDays(-7), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpireANameChangeNoModeratorLookedAtInTheReviewWindow()
    {
        var repository = Mock<IUsernameChangeRepository>();
        repository
            .ExpireUnreviewedRequests(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>()).Returns(1);

        await new UsernameChangeExpiryProcessor(repository, clock).ExpireUnreviewedAsync();

        await repository.Received(1).ExpireUnreviewedRequests(
            Now.AddDays(-7),
            Now,
            Arg.Is<string>(comment => comment.Contains("срок ожидания модерации")),
            Arg.Any<CancellationToken>());
    }

    // The approval deadline is stored per request rather than derived from a window, so what the
    // pass supplies is the moment to compare it against.
    [Fact]
    public async Task ExpireAnApprovalTheRequesterNeverUsed()
    {
        var repository = Mock<IUsernameChangeRepository>();
        repository
            .ExpireApprovalTokens(
                Arg.Any<DateTimeOffset>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1);

        await new UsernameChangeExpiryProcessor(repository, clock).ExpireApprovalTokensAsync();

        // The reason travels without a separator glued to it: approving takes no
        // comment, so it is normally the whole text the requester reads.
        await repository.Received(1).ExpireApprovalTokens(
            Now,
            Arg.Is<string>(reason => reason.StartsWith("Токен истек")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeSessionsThatHadExpiredByTheMomentThePassRuns()
    {
        var repository = Mock<IAuthenticationRepository>();
        repository
            .PurgeExpiredSessions(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new SessionPurgeResult(4));

        var purged = await new SessionCleanupProcessor(repository, clock).PurgeExpiredAsync();

        purged.SessionsRemoved.Should().Be(4);
        await repository.Received(1).PurgeExpiredSessions(Now, Arg.Any<CancellationToken>());
    }

    // A token has to be expired well before it is deleted: a reader following a stale link is told
    // the link expired, not that nothing was ever issued.
    [Fact]
    public void KeepATokenLongerThanAnyTokenIsValidFor()
    {
        var configuration = new TokenConfiguration();
        var longestLifetime = TimeSpan.FromHours(Math.Max(
            configuration.PasswordResetTokenLifetimeHours,
            Math.Max(configuration.ActivationTokenLifetimeHours, configuration.EmailChangeTokenLifetimeHours)));

        AccountRetentionPolicy.TokenRetention.Should().BeGreaterThan(longestLifetime);
    }
}
