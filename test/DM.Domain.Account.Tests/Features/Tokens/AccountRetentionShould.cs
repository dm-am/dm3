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
using FluentAssertions;
using Moq;
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

    private readonly Mock<IDateTimeProvider> _clock;

    public AccountRetentionShould()
    {
        _clock = Mock<IDateTimeProvider>();
        _clock.SetupGet(c => c.Now).Returns(Now);
    }

    [Fact]
    public async Task DropATokenOnceTheRetentionWindowHasPassed()
    {
        var repository = Mock<ITokenMaintenanceRepository>();
        repository
            .Setup(r => r.DeleteWithdrawnOrIssuedBefore(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var deleted = await new TokenCleanupProcessor(repository.Object, _clock.Object).DeleteStaleAsync();

        deleted.Should().Be(3);
        repository.Verify(r => r.DeleteWithdrawnOrIssuedBefore(
            Now - AccountRetentionPolicy.TokenRetention, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAnEmailAddressOnceTheRegistrationWindowHasPassed()
    {
        var repository = Mock<IRegistrationRepository>();
        repository
            .Setup(r => r.DeletePendingStartedBefore(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var deleted = await new PendingRegistrationCleanupProcessor(repository.Object, _clock.Object)
            .DeleteExpiredAsync();

        deleted.Should().Be(2);
        repository.Verify(r => r.DeletePendingStartedBefore(
            Now - AccountRetentionPolicy.PendingRegistrationLifetime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireANameChangeNoModeratorLookedAtInTheReviewWindow()
    {
        var repository = Mock<IUsernameChangeRepository>();
        repository
            .Setup(r => r.ExpireUnreviewedRequests(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await new UsernameChangeExpiryProcessor(repository.Object, _clock.Object).ExpireUnreviewedAsync();

        repository.Verify(r => r.ExpireUnreviewedRequests(
            Now - AccountRetentionPolicy.UsernameChangeReviewWindow,
            Now,
            It.Is<string>(comment => comment.Contains("срок ожидания модерации")),
            It.IsAny<CancellationToken>()), Times.Once);
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
            .Setup(r => r.ExpireApprovalTokens(
                It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await new UsernameChangeExpiryProcessor(repository.Object, _clock.Object).ExpireApprovalTokensAsync();

        repository.Verify(r => r.ExpireApprovalTokens(
            Now,
            It.Is<string>(comment => comment.StartsWith(" | ")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeSessionsThatHadExpiredByTheMomentThePassRuns()
    {
        var repository = Mock<IAuthenticationRepository>();
        repository
            .Setup(r => r.PurgeExpiredSessions(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionPurgeResult(4, 1));

        var purged = await new SessionCleanupProcessor(repository.Object, _clock.Object).PurgeExpiredAsync();

        purged.UsersTouched.Should().Be(4);
        purged.EmptyDocumentsRemoved.Should().Be(1);
        repository.Verify(r => r.PurgeExpiredSessions(Now, It.IsAny<CancellationToken>()), Times.Once);
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
