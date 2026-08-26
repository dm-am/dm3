using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

/// <summary>
/// What a login does when the account has a second factor.
/// </summary>
/// <remarks>
/// The whole point is stated by INV-7: between the two factors there is no
/// session. No row, no cookie, no activity stamp, no entry in the journal saying
/// somebody signed in. The alternative - a session marked "half" - obliges every
/// authorization surface in the product to remember the mark, and the day one of
/// them forgets, a password is enough again.
/// </remarks>
public class SecondFactorLoginShould : UnitTestBase
{
    private const string Email = "reader@example.com";
    private const string Password = "correct horse";
    private static readonly Guid UserId = Guid.Parse("77778888-9999-aaaa-bbbb-ccccddddeeee");
    private static readonly Guid ChallengeId = Guid.Parse("12341234-5678-4321-8765-987698769876");
    private static readonly Guid SessionId = Guid.Parse("abcdabcd-1234-4321-8888-999900001111");
    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ISecurityManager _securityManager;
    private readonly IAuthenticationRepository _repository;
    private readonly ISessionFactory _sessionFactory;
    private readonly ILoginAttemptTracker _loginAttemptTracker;
    private readonly ISecurityAuditRepository _auditService;
    private readonly ITwoFactorRepository _twoFactorRepository;
    private readonly ITwoFactorVerifier _twoFactorVerifier;
    private readonly ISymmetricCryptoService _cryptoService;
    private readonly AuthenticationService _service;
    private readonly AuthenticatedUser _user;

    public SecondFactorLoginShould()
    {
        _securityManager = Mock<ISecurityManager>();
        _repository = Mock<IAuthenticationRepository>();
        _sessionFactory = Mock<ISessionFactory>();
        _loginAttemptTracker = Mock<ILoginAttemptTracker>();
        _auditService = Mock<ISecurityAuditRepository>();
        _twoFactorRepository = Mock<ITwoFactorRepository>();
        _twoFactorVerifier = Mock<ITwoFactorVerifier>();
        _cryptoService = Mock<ISymmetricCryptoService>();
        var dateTimeProvider = Mock<IDateTimeProvider>();
        var guidFactory = Mock<IGuidFactory>();

        dateTimeProvider.Now.Returns(Moment);
        guidFactory.Create().Returns(ChallengeId);
        _cryptoService.Encrypt(Arg.Any<string>()).Returns("encrypted-token");

        _user = Create.User(UserId)
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        _user.Username = "reader";
        _user.Email = Email;

        _repository.IsPendingRegistration(Email).Returns(false);
        _repository.TryFindUserByEmail(Email).Returns((true, _user));
        _repository.FindUser(UserId).Returns(_user);
        _repository.FindUserSettings(UserId).Returns(UserSettings.Default);
        _repository.AddSession(UserId, Arg.Any<CreateSession>()).Returns(new Session { Id = SessionId });
        _sessionFactory.Create(Arg.Any<bool>(), Arg.Any<SessionContext?>())
            .Returns(new CreateSession { Id = SessionId });
        _securityManager.ComparePasswords(Password, "salt", "hash").Returns(true);

        _service = new AuthenticationService(
            _securityManager,
            _cryptoService,
            _repository,
            _sessionFactory,
            dateTimeProvider,
            Mock<IIdentityProvider>(),
            _loginAttemptTracker,
            _auditService,
            Mock<IEventProducer>(),
            _twoFactorRepository,
            _twoFactorVerifier,
            guidFactory,
            Mock<ILogger<AuthenticationService>>(),
            Options.Create(new AuthenticationConfiguration()),
            Options.Create(new TwoFactorConfiguration
            {
                ChallengeLifetimeMinutes = 5,
                ChallengeAttemptLimit = 5
            }));
    }

    private void FactorIsOn() =>
        _twoFactorRepository.IsConfirmed(UserId, Arg.Any<CancellationToken>()).Returns(true);

    private TwoFactorChallengeState Challenge(
        DateTimeOffset? expiresUtc = null, bool persistent = true) => new()
        {
            ChallengeId = ChallengeId,
            UserId = UserId,
            Account = Email,
            CreatedUtc = Moment,
            ExpiresUtc = expiresUtc ?? Moment.AddMinutes(5),
            Persistent = persistent
        };

    // ── The password step ──

    /// <summary>AC-1: an account without a factor signs in exactly as before.</summary>
    [Fact]
    public async Task LeaveALoginWithoutAFactorAsItWas()
    {
        var identity = await _service.Authenticate(Email, Password);

        identity.User.IsAuthenticated.Should().BeTrue();
        identity.TwoFactorChallengeId.Should().BeNull();
        await _repository.Received(1).AddSession(UserId, Arg.Any<CreateSession>());
    }

    /// <summary>AC-9 and INV-7: no session exists until the factor is shown.</summary>
    [Fact]
    public async Task CreateNoSessionForAnAccountWithAFactor()
    {
        FactorIsOn();

        var identity = await _service.Authenticate(Email, Password);

        identity.TwoFactorChallengeId.Should().Be(ChallengeId);
        identity.User.IsAuthenticated.Should().BeFalse();
        identity.AuthenticationToken.Should().BeNull();
        identity.Session.Should().BeNull();

        await _repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
        await _repository.DidNotReceive().UpdateActivity(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.LoginSuccess,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>
    /// The proven password does not reset the attempt counter on its own.
    /// </summary>
    /// <remarks>
    /// Otherwise anybody holding the password takes an unlimited supply of
    /// challenges, five guesses each, and the second level of the limit stops
    /// limiting anything.
    /// </remarks>
    [Fact]
    public async Task NotResetTheAttemptCounterUntilTheLoginIsFinished()
    {
        FactorIsOn();

        await _service.Authenticate(Email, Password);

        await _loginAttemptTracker.DidNotReceive().ResetAttempts(Arg.Any<string>());
    }

    [Fact]
    public async Task CarryTheContextOfTheFirstStepOntoTheChallenge()
    {
        FactorIsOn();
        var context = new SessionContext { IpAddress = "1.2.3.4", UserAgent = "agent" };

        await _service.Authenticate(Email, Password, rememberMe: false, context);

        await _twoFactorRepository.Received(1).AddChallenge(
            Arg.Is<TwoFactorChallengeState>(c =>
                c.UserId == UserId &&
                c.Account == Email &&
                !c.Persistent &&
                c.IpAddress == "1.2.3.4" &&
                c.ExpiresUtc == Moment.AddMinutes(5)),
            Arg.Any<CancellationToken>());
    }

    // ── The second step ──

    /// <summary>AC-10: the code finishes the login and the session is created.</summary>
    [Fact]
    public async Task FinishTheLoginOnAGoodCode()
    {
        ArrangeChallenge();

        var identity = await _service.CompleteSecondFactor(ChallengeId, "123456");

        identity.User.IsAuthenticated.Should().BeTrue();
        identity.Error.Should().Be(AuthenticationError.NoError);
        await _repository.Received(1).AddSession(UserId, Arg.Any<CreateSession>());
        await _twoFactorRepository.Received(1).RemoveChallenge(
            ChallengeId, Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.LoginSuccess,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>
    /// AC-10: "remember me" comes from the first step, where the box was.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CarryRememberMeFromTheFirstStep(bool persistent)
    {
        ArrangeChallenge(persistent: persistent);

        await _service.CompleteSecondFactor(ChallengeId, "123456");

        _sessionFactory.Received(1).Create(persistent, Arg.Any<SessionContext?>());
    }

    /// <summary>AC-14: five ways of failing, one answer.</summary>
    [Fact]
    public async Task AnswerEveryFailureTheSameWay()
    {
        // Unknown challenge
        _twoFactorRepository.FindChallenge(ChallengeId, Arg.Any<CancellationToken>())
            .Returns((TwoFactorChallengeState?)null);
        var unknown = await _service.CompleteSecondFactor(ChallengeId, "123456");

        // Expired challenge
        _twoFactorRepository.FindChallenge(ChallengeId, Arg.Any<CancellationToken>())
            .Returns(Challenge(expiresUtc: Moment.AddMinutes(-1)));
        var expired = await _service.CompleteSecondFactor(ChallengeId, "123456");

        // Wrong code
        ArrangeChallenge();
        _twoFactorVerifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(false);
        var wrong = await _service.CompleteSecondFactor(ChallengeId, "000000");

        // The factor went away between the steps
        ArrangeChallenge();
        _twoFactorRepository.Find(UserId, Arg.Any<CancellationToken>()).Returns((TwoFactorState?)null);
        var gone = await _service.CompleteSecondFactor(ChallengeId, "123456");

        new[] { unknown, expired, wrong, gone }.Should().AllSatisfy(identity =>
        {
            identity.Error.Should().Be(AuthenticationError.TwoFactorRejected);
            identity.User.IsAuthenticated.Should().BeFalse();
        });
    }

    /// <summary>AC-13: the challenge is destroyed once the attempts run out.</summary>
    [Fact]
    public async Task DestroyTheChallengeWhenTheAttemptsRunOut()
    {
        ArrangeChallenge();
        _twoFactorVerifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(false);
        _twoFactorRepository.CountChallengeAttempt(ChallengeId, Arg.Any<CancellationToken>())
            .Returns(1, 2, 3, 4, 5);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await _service.CompleteSecondFactor(ChallengeId, "000000");
        }

        await _twoFactorRepository.DidNotReceive().RemoveChallenge(
            ChallengeId, Arg.Any<CancellationToken>());

        await _service.CompleteSecondFactor(ChallengeId, "000000");

        await _twoFactorRepository.Received(1).RemoveChallenge(
            ChallengeId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A wrong code is counted where a wrong password is counted.
    /// </summary>
    /// <remarks>
    /// Two counters over one login means two thresholds, and only the lower of
    /// them would ever be reached.
    /// </remarks>
    [Fact]
    public async Task CountAWrongCodeInTheCounterOfThePasswordStep()
    {
        ArrangeChallenge();
        _twoFactorVerifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(false);
        var context = new SessionContext { IpAddress = "1.2.3.4" };

        await _service.CompleteSecondFactor(ChallengeId, "000000", context);

        await _loginAttemptTracker.Received(1).RecordFailedAttempt(
            new LoginAttemptOrigin(Email, "1.2.3.4"));
    }

    [Fact]
    public async Task RefuseTheSecondStepWhileTheAccountIsLockedOut()
    {
        ArrangeChallenge();
        _loginAttemptTracker.IsAccountLocked(Arg.Any<LoginAttemptOrigin>()).Returns(true);

        var identity = await _service.CompleteSecondFactor(ChallengeId, "123456");

        identity.Error.Should().Be(AuthenticationError.TwoFactorRejected);
        await _repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    /// <summary>AC-17: a ban issued between the two steps takes effect.</summary>
    [Fact]
    public async Task RefuseALoginBannedBetweenTheTwoSteps()
    {
        ArrangeChallenge();
        _user.AccessRestrictions =
            [new AccessRestriction(AccessPolicy.FullBan, Moment.AddMinutes(-1), Moment.AddDays(1))];

        var identity = await _service.CompleteSecondFactor(ChallengeId, "123456");

        identity.Error.Should().Be(AuthenticationError.Banned);
        await _repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    [Fact]
    public async Task RefuseALoginRemovedBetweenTheTwoSteps()
    {
        ArrangeChallenge();
        _user.IsRemoved = true;

        var identity = await _service.CompleteSecondFactor(ChallengeId, "123456");

        identity.Error.Should().Be(AuthenticationError.Removed);
        await _repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    /// <summary>AC-23: a sign-in with the factor calls off a scheduled removal.</summary>
    [Fact]
    public async Task CallOffAScheduledRemovalOnASuccessfulSecondFactor()
    {
        ArrangeChallenge();
        _twoFactorRepository.CancelScheduledRemoval(UserId, Arg.Any<CancellationToken>())
            .Returns(true);

        await _service.CompleteSecondFactor(ChallengeId, "123456");

        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovalCancelled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task SayNothingAboutACancellationWhenNoRemovalWasPending()
    {
        ArrangeChallenge();
        _twoFactorRepository.CancelScheduledRemoval(UserId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.CompleteSecondFactor(ChallengeId, "123456");

        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorRemovalCancelled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    // ── The path that mints a session without a password ──

    /// <summary>
    /// INV-19 and AC-29: no session without a password for an account with a
    /// factor.
    /// </summary>
    /// <remarks>
    /// Today the only caller is the auto-login that follows activation, and a
    /// freshly created account cannot have a factor - which makes the rule safe
    /// by circumstance rather than by construction. The next caller arrives
    /// without a word.
    /// </remarks>
    [Fact]
    public async Task RefuseToMintASessionWithoutAPasswordForAnAccountWithAFactor()
    {
        FactorIsOn();

        var identity = await _service.Authenticate(UserId);

        identity.User.IsAuthenticated.Should().BeFalse();
        identity.Error.Should().Be(AuthenticationError.Forbidden);
        await _repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    [Fact]
    public async Task StillMintASessionWithoutAPasswordForAnAccountWithoutAFactor()
    {
        var identity = await _service.Authenticate(UserId);

        identity.User.IsAuthenticated.Should().BeTrue();
        await _repository.Received(1).AddSession(UserId, Arg.Any<CreateSession>());
    }

    // ── The fold on the token path ──

    /// <summary>
    /// INV-10: the fold happens where the identity is built, so every resolver
    /// and every hand-written rank comparison reads the withheld role.
    /// </summary>
    [Fact]
    public async Task WithholdTheRankOnTheTokenPathWhileTheFactorIsMissing()
    {
        ArrangeTokenPath(UserRole.Admin, factorConfirmed: false);

        var identity = await _service.Authenticate("token");

        identity.User.IsAuthenticated.Should().BeTrue("the factor never refuses a login");
        identity.User.Role.Should().Be(UserRole.RegularUser);
        identity.User.PrivilegeWithheld.Should().BeTrue();
        identity.User.RecordedRole.Should().Be(UserRole.Admin);
    }

    /// <summary>AC-20: the rank comes back on the very next request.</summary>
    [Fact]
    public async Task GiveTheRankBackOnTheNextRequestOnceTheFactorIsOn()
    {
        ArrangeTokenPath(UserRole.Admin, factorConfirmed: true);

        var identity = await _service.Authenticate("token");

        identity.User.Role.Should().Be(UserRole.Admin);
        identity.User.PrivilegeWithheld.Should().BeFalse();
    }

    /// <summary>
    /// The storage is only asked about the ranks the requirement covers.
    /// </summary>
    /// <remarks>
    /// The fold runs on every authenticated request, and for everybody outside
    /// those two ranks the answer is decided without leaving the process.
    /// </remarks>
    [Fact]
    public async Task AskStorageAboutTheFactorOnlyForARankThatOwesOne()
    {
        ArrangeTokenPath(UserRole.RegularUser, factorConfirmed: false);

        await _service.Authenticate("token");

        await _twoFactorRepository.DidNotReceive().IsConfirmed(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private void ArrangeTokenPath(UserRole role, bool factorConfirmed)
    {
        _user.Role = role;
        _twoFactorRepository.IsConfirmed(UserId, Arg.Any<CancellationToken>()).Returns(factorConfirmed);
        _repository.FindUserSession(UserId, SessionId)
            .Returns(new Session { Id = SessionId, ExpirationUtc = Moment.AddDays(1) });
        _cryptoService.Decrypt("token")
            .Returns($"{{\"userId\":\"{UserId}\",\"sessionId\":\"{SessionId}\"}}");
    }

    private void ArrangeChallenge(bool persistent = true)
    {
        _twoFactorRepository.FindChallenge(ChallengeId, Arg.Any<CancellationToken>())
            .Returns(Challenge(persistent: persistent));
        _twoFactorRepository.Find(UserId, Arg.Any<CancellationToken>()).Returns(new TwoFactorState
        {
            UserId = UserId,
            Secret = "envelope",
            CreatedUtc = Moment.AddDays(-1),
            ConfirmedUtc = Moment.AddDays(-1)
        });
        _twoFactorVerifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);
        _loginAttemptTracker.IsAccountLocked(Arg.Any<LoginAttemptOrigin>()).Returns(false);
    }
}
