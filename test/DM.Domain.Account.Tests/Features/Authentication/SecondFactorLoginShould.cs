using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Account.Tests.Dsl;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

// Between the two factors there is no session, so no authorization surface has to remember a half
// one.
public class SecondFactorLoginShould : AuthenticationServiceTestBase
{
    private const string Email = "reader@example.com";
    private const string Password = "correct horse";
    private static readonly Guid UserId = Guid.Parse("77778888-9999-aaaa-bbbb-ccccddddeeee");
    private static readonly Guid ChallengeId = Guid.Parse("12341234-5678-4321-8765-987698769876");
    private static readonly Guid SessionId = Guid.Parse("abcdabcd-1234-4321-8888-999900001111");
    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly AuthenticatedUser user;

    public SecondFactorLoginShould()
    {
        dateTimeProvider.Now.Returns(Moment);
        guidFactory.Create().Returns(ChallengeId);
        cryptoService.Encrypt(Arg.Any<string>()).Returns("encrypted-token");

        user = Create.User(UserId)
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        user.Username = "reader";
        user.Email = Email;

        repository.TryFindUserByEmail(Email).Returns((true, user));
        repository.FindUser(UserId).Returns(user);
        repository.FindUserSettings(UserId).Returns(UserSettings.Default);
        repository.AddSession(UserId, Arg.Any<CreateSession>()).Returns(new Session { Id = SessionId });
        sessionFactory.Create(Arg.Any<bool>(), Arg.Any<SessionContext?>())
            .Returns(new CreateSession { Id = SessionId });
        securityManager.ComparePasswords(Password, "salt", "hash").Returns(true);
    }

    private void FactorIsOn() =>
        twoFactorRepository.IsConfirmed(UserId, Arg.Any<CancellationToken>()).Returns(true);

    private static TwoFactorChallengeState Challenge(
        DateTimeOffset? expiresUtc = null, bool persistent = true) => new()
        {
            ChallengeId = ChallengeId,
            UserId = UserId,
            Account = Email,
            CreatedUtc = Moment,
            ExpiresUtc = expiresUtc ?? Moment.AddMinutes(5),
            Persistent = persistent
        };

    /// <summary>An account without a factor signs in exactly as before.</summary>
    [Fact]
    public async Task LeaveALoginWithoutAFactorAsItWas()
    {
        var identity = await service.Authenticate(Email, Password);

        identity.User.IsAuthenticated.Should().BeTrue();
        identity.TwoFactorChallengeId.Should().BeNull();
        await repository.Received(1).AddSession(UserId, Arg.Any<CreateSession>());
    }

    [Fact]
    public async Task CreateNoSessionForAnAccountWithAFactor()
    {
        FactorIsOn();

        var identity = await service.Authenticate(Email, Password);

        identity.TwoFactorChallengeId.Should().Be(ChallengeId);
        identity.User.IsAuthenticated.Should().BeFalse();
        identity.AuthenticationToken.Should().BeNull();
        identity.Session.Should().BeNull();

        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
        await repository.DidNotReceive().UpdateActivity(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.LoginSuccess,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    // Otherwise the password alone buys an unlimited supply of challenges.
    [Fact]
    public async Task NotResetTheAttemptCounterUntilTheLoginIsFinished()
    {
        FactorIsOn();

        await service.Authenticate(Email, Password);

        await loginAttemptTracker.DidNotReceive().ResetAttempts(Arg.Any<string>());
    }

    [Fact]
    public async Task CarryTheContextOfTheFirstStepOntoTheChallenge()
    {
        FactorIsOn();
        var context = new SessionContext { IpAddress = "1.2.3.4", UserAgent = "agent" };

        await service.Authenticate(Email, Password, rememberMe: false, context);

        await twoFactorRepository.Received(1).AddChallenge(
            Arg.Is<TwoFactorChallengeState>(c =>
                c.UserId == UserId &&
                c.Account == Email &&
                !c.Persistent &&
                c.IpAddress == "1.2.3.4" &&
                c.ExpiresUtc == Moment.AddMinutes(5)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinishTheLoginOnAGoodCode()
    {
        ArrangeChallenge();

        var identity = await service.CompleteSecondFactor(ChallengeId, "123456");

        identity.User.IsAuthenticated.Should().BeTrue();
        identity.Error.Should().Be(AuthenticationError.NoError);
        await repository.Received(1).AddSession(UserId, Arg.Any<CreateSession>());
        await twoFactorRepository.Received(1).RemoveChallenge(
            ChallengeId, Arg.Any<CancellationToken>());
        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.LoginSuccess,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    // "Remember me" comes from the first step, where the box was.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CarryRememberMeFromTheFirstStep(bool persistent)
    {
        ArrangeChallenge(persistent: persistent);

        await service.CompleteSecondFactor(ChallengeId, "123456");

        sessionFactory.Received(1).Create(persistent, Arg.Any<SessionContext?>());
    }

    [Fact]
    public async Task AnswerEveryFailureTheSameWay()
    {
        // Unknown challenge
        twoFactorRepository.FindChallenge(ChallengeId, Arg.Any<CancellationToken>())
            .Returns((TwoFactorChallengeState?)null);
        var unknown = await service.CompleteSecondFactor(ChallengeId, "123456");

        // Expired challenge
        twoFactorRepository.FindChallenge(ChallengeId, Arg.Any<CancellationToken>())
            .Returns(Challenge(expiresUtc: Moment.AddMinutes(-1)));
        var expired = await service.CompleteSecondFactor(ChallengeId, "123456");

        // Wrong code
        ArrangeChallenge();
        twoFactorVerifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(false);
        var wrong = await service.CompleteSecondFactor(ChallengeId, "000000");

        // The factor went away between the steps
        ArrangeChallenge();
        twoFactorRepository.Find(UserId, Arg.Any<CancellationToken>()).Returns((TwoFactorState?)null);
        var gone = await service.CompleteSecondFactor(ChallengeId, "123456");

        new[] { unknown, expired, wrong, gone }.Should().AllSatisfy(identity =>
        {
            identity.Error.Should().Be(AuthenticationError.TwoFactorRejected);
            identity.User.IsAuthenticated.Should().BeFalse();
        });
    }

    [Fact]
    public async Task DestroyTheChallengeWhenTheAttemptsRunOut()
    {
        ArrangeChallenge();
        twoFactorVerifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(false);
        twoFactorRepository.CountChallengeAttempt(ChallengeId, Arg.Any<CancellationToken>())
            .Returns(1, 2, 3, 4, 5);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await service.CompleteSecondFactor(ChallengeId, "000000");
        }

        await twoFactorRepository.DidNotReceive().RemoveChallenge(
            ChallengeId, Arg.Any<CancellationToken>());

        await service.CompleteSecondFactor(ChallengeId, "000000");

        await twoFactorRepository.Received(1).RemoveChallenge(
            ChallengeId, Arg.Any<CancellationToken>());
    }

    // Two counters over one login means two thresholds, and only the lower of them would ever be
    // reached.
    [Fact]
    public async Task CountAWrongCodeInTheCounterOfThePasswordStep()
    {
        ArrangeChallenge();
        twoFactorVerifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(false);
        var context = new SessionContext { IpAddress = "1.2.3.4" };

        await service.CompleteSecondFactor(ChallengeId, "000000", context);

        await loginAttemptTracker.Received(1).RecordFailedAttempt(
            new LoginAttemptOrigin(Email, "1.2.3.4"));
    }

    [Fact]
    public async Task RefuseTheSecondStepWhileTheAccountIsLockedOut()
    {
        ArrangeChallenge();
        loginAttemptTracker.IsAccountLocked(Arg.Any<LoginAttemptOrigin>()).Returns(true);

        var identity = await service.CompleteSecondFactor(ChallengeId, "123456");

        identity.Error.Should().Be(AuthenticationError.TwoFactorRejected);
        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    [Fact]
    public async Task RefuseALoginBannedBetweenTheTwoSteps()
    {
        ArrangeChallenge();
        user.AccessRestrictions =
            [new AccessRestriction(AccessPolicy.FullBan, Moment.AddMinutes(-1), Moment.AddDays(1))];

        var identity = await service.CompleteSecondFactor(ChallengeId, "123456");

        identity.Error.Should().Be(AuthenticationError.Banned);
        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    [Fact]
    public async Task RefuseALoginRemovedBetweenTheTwoSteps()
    {
        ArrangeChallenge();
        user.IsRemoved = true;

        var identity = await service.CompleteSecondFactor(ChallengeId, "123456");

        identity.Error.Should().Be(AuthenticationError.Removed);
        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    [Fact]
    public async Task CallOffAScheduledRemovalOnASuccessfulSecondFactor()
    {
        ArrangeChallenge();
        twoFactorRepository.CancelScheduledRemoval(UserId, Arg.Any<CancellationToken>())
            .Returns(true);

        await service.CompleteSecondFactor(ChallengeId, "123456");

        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovalCancelled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task SayNothingAboutACancellationWhenNoRemovalWasPending()
    {
        ArrangeChallenge();
        twoFactorRepository.CancelScheduledRemoval(UserId, Arg.Any<CancellationToken>())
            .Returns(false);

        await service.CompleteSecondFactor(ChallengeId, "123456");

        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorRemovalCancelled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    // Safe by construction, not by the circumstance that today's only caller is a fresh account.
    [Fact]
    public async Task RefuseToMintASessionWithoutAPasswordForAnAccountWithAFactor()
    {
        FactorIsOn();

        var identity = await service.Authenticate(UserId);

        identity.User.IsAuthenticated.Should().BeFalse();
        identity.Error.Should().Be(AuthenticationError.Forbidden);
        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    [Fact]
    public async Task StillMintASessionWithoutAPasswordForAnAccountWithoutAFactor()
    {
        var identity = await service.Authenticate(UserId);

        identity.User.IsAuthenticated.Should().BeTrue();
        await repository.Received(1).AddSession(UserId, Arg.Any<CreateSession>());
    }

    // A second factor is optional for every rank: an account without one acts with the rank
    // recorded on it.
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public async Task KeepTheRankOnTheTokenPathWithoutAFactor(UserRole role)
    {
        ArrangeTokenPath(role, factorConfirmed: false);

        var identity = await service.Authenticate("token");

        identity.User.IsAuthenticated.Should().BeTrue();
        identity.User.Role.Should().Be(role);
    }

    // An ordinary request never asks the storage about the factor: it is asked at sign-in, where it
    // is owed, and nowhere else.
    [Fact]
    public async Task NotAskStorageAboutTheFactorOnTheTokenPath()
    {
        ArrangeTokenPath(UserRole.Admin, factorConfirmed: true);

        await service.Authenticate("token");

        await twoFactorRepository.DidNotReceive().IsConfirmed(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private void ArrangeTokenPath(UserRole role, bool factorConfirmed)
    {
        user.Role = role;
        twoFactorRepository.IsConfirmed(UserId, Arg.Any<CancellationToken>()).Returns(factorConfirmed);
        repository.FindUserSession(UserId, SessionId)
            .Returns(new Session { Id = SessionId, ExpirationUtc = Moment.AddDays(1) });
        cryptoService.Decrypt("token")
            .Returns($"{{\"userId\":\"{UserId}\",\"sessionId\":\"{SessionId}\"}}");
    }

    private void ArrangeChallenge(bool persistent = true)
    {
        twoFactorRepository.FindChallenge(ChallengeId, Arg.Any<CancellationToken>())
            .Returns(Challenge(persistent: persistent));
        twoFactorRepository.Find(UserId, Arg.Any<CancellationToken>()).Returns(new TwoFactorState
        {
            UserId = UserId,
            Secret = "envelope",
            CreatedUtc = Moment.AddDays(-1),
            ConfirmedUtc = Moment.AddDays(-1)
        });
        twoFactorVerifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);
        loginAttemptTracker.IsAccountLocked(Arg.Any<LoginAttemptOrigin>()).Returns(false);
    }
}
