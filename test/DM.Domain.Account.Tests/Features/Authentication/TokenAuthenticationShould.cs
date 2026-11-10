using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Tests.Dsl;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

/// <summary>
/// What the two paths that hand out an identity without a password answer to:
/// the session token on every request, and the direct sign-in after activation.
/// </summary>
/// <remarks>
/// Both read the state of the account afresh, so a ban or a removal takes effect
/// on the next request rather than at session expiry; the token also looks its
/// session up under the user it names and refreshes it no further than the
/// ceiling it was issued with.
/// </remarks>
public class TokenAuthenticationShould : AuthenticationServiceTestBase
{
    private static readonly Guid UserId = Guid.Parse("7b1c2d3e-4f50-4a61-8b72-9c83d4e5f607");
    private static readonly Guid SessionId = Guid.Parse("1a2b3c4d-5e6f-4071-8293-a4b5c6d7e8f9");

    [Fact]
    public async Task RefuseTokenOfFullyBannedUser()
    {
        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.FullBan)
            .Please();
        Token(user, new Session { Id = Guid.NewGuid(), ExpirationUtc = DateTime.UtcNow.AddDays(1) });

        var result = await service.Authenticate("token");

        // A ban applied after the cookie was issued must take effect on the very
        // next request, not at session expiry
        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Banned);
    }

    // The session is a day from its end and the account has no activity stamp, so a token path that
    // let the account through would both refresh the session and stamp the account: the refusal has
    // to stop before either.
    [Fact]
    public async Task RefuseTokenOfARemovedAccount()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        user.IsRemoved = true;
        user.LastActivityUtc = null;
        Token(user, new Session
        {
            Id = Guid.NewGuid(),
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            ExpirationUtc = DateTimeOffset.UtcNow.AddMinutes(10),
            Persistent = true
        });

        var result = await service.Authenticate("token");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Removed);
        await repository.DidNotReceive().RefreshSession(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
        await repository.DidNotReceive().UpdateActivity(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
    }

    /// <summary>
    /// The path that mints a session without a password answers to the state of
    /// the account like the other two do.
    /// </summary>
    /// <remarks>
    /// Everything a session needs is stubbed, so the refusal holds against an
    /// account that would otherwise be signed in. Nothing in the signature of
    /// the path says which callers reach it, so the rule has to be in the path.
    /// </remarks>
    [Theory]
    [InlineData(true, AccessPolicy.NotSpecified, AuthenticationError.Removed)]
    [InlineData(false, AccessPolicy.FullBan, AuthenticationError.Banned)]
    public async Task RefuseADirectSignInToARemovedOrBannedAccount(
        bool isRemoved, AccessPolicy accessPolicy, AuthenticationError expected)
    {
        var userId = Guid.NewGuid();
        var user = Create.User(userId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(accessPolicy)
            .Please();
        user.IsRemoved = isRemoved;

        repository.FindUser(userId).Returns(user);
        repository.FindUserSettings(userId).Returns(UserSettings.Default);
        sessionFactory.Create(Arg.Any<bool>(), Arg.Any<SessionContext?>())
            .Returns(new CreateSession { Id = Guid.NewGuid() });
        repository.AddSession(userId, Arg.Any<CreateSession>()).Returns(new Session { Id = Guid.NewGuid() });
        cryptoService.Encrypt(Arg.Any<string>()).Returns("encrypted-token");

        var result = await service.Authenticate(userId);

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(expected);
        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
        await repository.DidNotReceive().UpdateActivity(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
        await auditService.DidNotReceive().LogAsync(userId, SecurityEventType.LoginSuccess,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task LookUpTheSessionUnderTheUserFromTheToken()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        Token(Create.User(userId).WithRole(UserRole.RegularUser).Please(),
            new Session { Id = sessionId, ExpirationUtc = DateTime.UtcNow.AddDays(1) });

        var result = await service.Authenticate("token");

        result.User.IsAuthenticated.Should().BeTrue();
        // Both halves of the token are used together. Looking the session up by
        // its id alone would authenticate a token whose userId and sessionId
        // belong to different people.
        await repository.Received(1).FindUserSession(userId, sessionId);
    }

    [Fact]
    public async Task RefuseTokenWhoseSessionBelongsToSomebodyElse()
    {
        // The session exists in the store, but not under this user
        Token(Create.User().WithRole(UserRole.RegularUser).Please(), session: null);

        var result = await service.Authenticate("token");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.SessionExpired);
    }

    [Fact]
    public async Task ApplyABanRowToTheIdentityItBuilds()
    {
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);

        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        // The ban lives in its own table and reaches the identity as a restriction
        // with a window; the user's own column stays NotSpecified forever
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.DemocraticBan, now.AddDays(-1), now.AddDays(1))
        ];
        Token(user, new Session { Id = Guid.NewGuid(), ExpirationUtc = now.AddDays(1).UtcDateTime });

        var result = await service.Authenticate("token");

        result.User.IsAuthenticated.Should().BeTrue();
        // Every authorization resolver reads AccessPolicy. Without the fold the
        // ban would be invisible to all of them.
        result.User.AccessPolicy.Should().HaveFlag(AccessPolicy.DemocraticBan);
    }

    [Theory]
    [InlineData(-10, -5)] // ban already served
    [InlineData(5, 10)]   // ban scheduled but not started
    public async Task IgnoreABanThatIsNotInForce(int startsInDays, int endsInDays)
    {
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);

        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.DemocraticBan, now.AddDays(startsInDays), now.AddDays(endsInDays))
        ];
        Token(user, new Session { Id = Guid.NewGuid(), ExpirationUtc = now.AddDays(1).UtcDateTime });

        var result = await service.Authenticate("token");

        // The row stays for the moderation history; it must stop restricting the
        // moment it stops being in force, with no job to run
        result.User.AccessPolicy.Should().Be(AccessPolicy.NotSpecified);
    }

    /// <summary>
    /// A session has a last day, and refreshing it does not move that day.
    /// </summary>
    /// <remarks>
    /// The sliding window extended the session from its own expiry, so a token used
    /// once a week never expired at all: the account it belongs to could be renamed,
    /// have its password changed and be forgotten, and the token would still open a
    /// session. The ceiling is the lifetime the session was issued with, counted from
    /// issue - which is a number the configuration already states rather than a third
    /// one nobody chose.
    /// </remarks>
    [Fact]
    public async Task RefreshASessionNoFurtherThanTheDayItWasIssuedFor()
    {
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        dateTimeProvider.Now.Returns(now);

        // Issued 23 hours ago against a 24 hour lifetime and inside the refresh
        // window: an unbounded slide would push it half an hour past the ceiling.
        var session = new Session
        {
            Id = SessionId,
            CreatedUtc = now.AddHours(-23),
            ExpirationUtc = now.AddMinutes(30),
            Persistent = false,
        };

        await AuthenticateWith(session);

        // The ceiling is the lifetime the session was issued with, counted from issue.
        await repository.Received(1).RefreshSession(
            UserId, SessionId, session.CreatedUtc.AddHours(24));
    }

    [Fact]
    public async Task StopRefreshingASessionThatHasReachedItsCeiling()
    {
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        dateTimeProvider.Now.Returns(now);

        // Already at the ceiling: every request of the last window would otherwise
        // write the same value again.
        var session = new Session
        {
            Id = SessionId,
            CreatedUtc = now.AddHours(-23),
            ExpirationUtc = now.AddHours(-23).AddHours(24),
            Persistent = false,
        };

        await AuthenticateWith(session);

        await repository.DidNotReceive().RefreshSession(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
    }

    private async Task AuthenticateWith(Session session)
    {
        var user = Create.User(UserId).WithRole(UserRole.RegularUser).Please();
        user.LastActivityUtc = session.CreatedUtc;
        Token(user, session);

        await service.Authenticate("token");
    }

    // The token "token" names the user and a session; the repository finds the user and, under him,
    // the session given (none for null).
    private void Token(AuthenticatedUser user, Session? session)
    {
        var sessionId = session?.Id ?? Guid.NewGuid();
        cryptoService.Decrypt("token").Returns($"{{\"userId\":\"{user.UserId}\",\"sessionId\":\"{sessionId}\"}}");
        repository.FindUser(user.UserId).Returns(user);
        repository.FindUserSettings(user.UserId).Returns(UserSettings.Default);
        repository.FindUserSession(user.UserId, sessionId).Returns(session);
    }
}
