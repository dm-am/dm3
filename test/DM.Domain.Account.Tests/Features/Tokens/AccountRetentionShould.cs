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

/// <summary>
/// How long the account area keeps what has served its purpose.
/// </summary>
/// <remarks>
/// Four windows, and every one of them used to be a literal inside a background
/// job of the HTTP host: seven days of token retention, seven days of a pending
/// registration holding an email address, seven days for a moderator to look at
/// a name change, and the approval a requester never used. Nothing outside a
/// running host could read them, call them or test them, and the middle one is
/// not a cleanup detail at all — until it passes, the address is not free for
/// anybody else to register.
///
/// Each processor is asked for one pass and the cutoff it hands the store is
/// what is checked, against the clock it was given rather than the machine's.
/// </remarks>
public class AccountRetentionShould : UnitTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IDateTimeProvider _clock;

    public AccountRetentionShould()
    {
        _clock = Mock<IDateTimeProvider>();
        _clock.Now.Returns(Now);
    }

    [Fact]
    public async Task DropATokenOnceTheRetentionWindowHasPassed()
    {
        var repository = Mock<ITokenMaintenanceRepository>();
        repository
            .DeleteWithdrawnOrIssuedBefore(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(3);

        var deleted = await new TokenCleanupProcessor(repository, _clock).DeleteStaleAsync();

        deleted.Should().Be(3);
        await repository.Received(1).DeleteWithdrawnOrIssuedBefore(
            Now - AccountRetentionPolicy.TokenRetention, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReleaseAnEmailAddressOnceTheRegistrationWindowHasPassed()
    {
        var repository = Mock<IRegistrationRepository>();
        repository
            .DeletePendingStartedBefore(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(2);

        var deleted = await new PendingRegistrationCleanupProcessor(repository, _clock)
            .DeleteExpiredAsync();

        deleted.Should().Be(2);
        await repository.Received(1).DeletePendingStartedBefore(
            Now - AccountRetentionPolicy.PendingRegistrationLifetime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpireANameChangeNoModeratorLookedAtInTheReviewWindow()
    {
        var repository = Mock<IUsernameChangeRepository>();
        repository
            .ExpireUnreviewedRequests(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>()).Returns(1);

        await new UsernameChangeExpiryProcessor(repository, _clock).ExpireUnreviewedAsync();

        await repository.Received(1).ExpireUnreviewedRequests(
            Now - AccountRetentionPolicy.UsernameChangeReviewWindow,
            Now,
            Arg.Is<string>(comment => comment.Contains("срок ожидания модерации")),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The approval deadline is stored per request rather than derived from a
    /// window, so what the pass supplies is the moment to compare it against.
    /// </summary>
    [Fact]
    public async Task ExpireAnApprovalTheRequesterNeverUsed()
    {
        var repository = Mock<IUsernameChangeRepository>();
        repository
            .ExpireApprovalTokens(
                Arg.Any<DateTimeOffset>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1);

        await new UsernameChangeExpiryProcessor(repository, _clock).ExpireApprovalTokensAsync();

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

        var purged = await new SessionCleanupProcessor(repository, _clock).PurgeExpiredAsync();

        purged.SessionsRemoved.Should().Be(4);
        await repository.Received(1).PurgeExpiredSessions(Now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A token has to be expired well before it is deleted: a reader following a
    /// stale link is told the link expired, not that nothing was ever issued.
    /// </summary>
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
