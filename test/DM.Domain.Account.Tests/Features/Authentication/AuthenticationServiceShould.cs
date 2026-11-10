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

public class AuthenticationServiceShould : AuthenticationServiceTestBase
{
    [Fact]
    public async Task ReturnFailureWhenEmailIsPendingRegistration()
    {
        var email = "test@example.com";
        repository.IsPendingRegistration(email).Returns(true);

        var result = await service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.PendingRegistration);
    }

    [Fact]
    public async Task ReturnFailureWhenAccountIsLocked()
    {
        var email = "test@example.com";
        loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(true);
        loginAttemptTracker.GetRemainingLockoutSeconds(new LoginAttemptOrigin(email, null)).Returns(300);

        var result = await service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.AccountLocked);
        // The wait travels with the refusal: the screen spells it out of this
        // number, next to the title.
        result.LockoutSecondsRemaining.Should().Be(300);
    }

    [Fact]
    public async Task ReturnFailureWhenUserNotFound()
    {
        var email = "test@example.com";
        repository.TryFindUserByEmail(email).Returns(((bool, AuthenticatedUser?))(false, null));

        var result = await service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.WrongLogin);
        await loginAttemptTracker.Received(1).RecordFailedAttempt(new LoginAttemptOrigin(email, null));
        // Answered in the same time as a wrong password, not only in the same
        // words: with no hash of anything, a missing account comes back before
        // Argon2id would have finished, and that difference is the answer
        securityManager.Received(1).ComparePasswords(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReturnFailureWhenPasswordIsWrong()
    {
        var email = "test@example.com";
        var user = Create.User().WithCredentials("salt", "hash").Please();

        repository.TryFindUserByEmail(email).Returns((true, user));
        securityManager.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash).Returns(false);

        var result = await service.Authenticate(email, "wrongpassword");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.WrongPassword);
        await loginAttemptTracker.Received(1).RecordFailedAttempt(new LoginAttemptOrigin(email, null));
        await auditService.Received(1).LogAsync(user.UserId, SecurityEventType.LoginFailure,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task AnnounceTheLockoutOnTheAttemptThatCausesIt()
    {
        var email = "test@example.com";
        var user = Create.User().WithCredentials("salt", "hash").Please();
        var origin = new LoginAttemptOrigin(email, null);

        repository.TryFindUserByEmail(email).Returns((true, user));
        securityManager.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash).Returns(false);

        // Unlocked when the attempt starts, locked once it has been counted:
        // that is the one attempt the notification belongs to.
        loginAttemptTracker.IsAccountLocked(origin).Returns(false, true);

        await service.Authenticate(email, "wrongpassword");

        await eventProducer.Received(1).SendAsync(EventType.AccountLocked, user.UserId);
    }

    [Fact]
    public async Task AuthenticateSuccessfullyWithValidCredentials()
    {
        var email = "test@example.com";
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).WithCredentials("salt", "hash").Please();
        var createSession = new CreateSession { Id = sessionId };
        var session = new Session { Id = sessionId };
        var settings = UserSettings.Default;

        repository.TryFindUserByEmail(email).Returns((true, user));
        securityManager.ComparePasswords("password", user.Salt, user.PasswordHash).Returns(true);
        sessionFactory.Create(true, null).Returns(createSession);
        repository.FindUserSettings(userId).Returns(settings);
        repository.AddSession(userId, createSession).Returns(session);
        cryptoService.Encrypt(Arg.Any<string>()).Returns("encrypted-token");

        var result = await service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeTrue();
        result.User.UserId.Should().Be(userId);
        await loginAttemptTracker.Received(1).ResetAttempts(email);
        await repository.Received(1).UpdateActivity(userId, Arg.Any<DateTimeOffset>());
        await auditService.Received(1).LogAsync(userId, SecurityEventType.LoginSuccess,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseLoginForFullyBannedUser()
    {
        var email = "banned@example.com";
        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.FullBan)
            .WithCredentials("salt", "hash")
            .Please();

        repository.TryFindUserByEmail(email).Returns((true, user));
        // The password is right, and that is what earns the real reason: to
        // everyone else a banned account answers like a wrong password, see
        // HideTheStateOfTheAccountUntilThePasswordIsProven
        securityManager.ComparePasswords("password", user.Salt, user.PasswordHash).Returns(true);

        var result = await service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Banned);
        // No session is minted for a banned account
        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
        // A refusal after a proven password is the one line in the journal
        // that says the password itself is known to somebody (SECURITY.md).
        await auditService.Received(1).LogAsync(user.UserId, SecurityEventType.LoginFailure,
            Arg.Any<string?>(), Arg.Any<string?>(), "Account banned");
    }

    /// <summary>
    /// A wrong password answers the same whatever state the account is in.
    /// </summary>
    /// <remarks>
    /// "Removed" and "banned" used to be decided before the password was
    /// compared, so anyone who typed an address learned what moderation had done
    /// to the person behind it, with no password and with nothing counted
    /// against the asking. That an address is registered is disclosed here by a
    /// recorded decision (SECURITY.md, the enumeration exception); what was done
    /// with the account is not.
    /// </remarks>
    [Theory]
    [InlineData(true, AccessPolicy.NotSpecified)]
    [InlineData(false, AccessPolicy.FullBan)]
    public async Task HideTheStateOfTheAccountUntilThePasswordIsProven(
        bool isRemoved, AccessPolicy accessPolicy)
    {
        var email = "test@example.com";
        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(accessPolicy)
            .WithCredentials("salt", "hash")
            .Please();
        user.IsRemoved = isRemoved;
        var origin = new LoginAttemptOrigin(email, null);

        repository.TryFindUserByEmail(email).Returns((true, user));
        securityManager.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash).Returns(false);

        var result = await service.Authenticate(email, "wrongpassword");

        result.Error.Should().Be(AuthenticationError.WrongPassword);
        // And it costs the guess an attempt, like any other wrong password does
        await loginAttemptTracker.Received(1).RecordFailedAttempt(origin);
    }

    [Fact]
    public async Task RefuseLoginForARemovedAccountOnceThePasswordIsProven()
    {
        var email = "removed@example.com";
        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        user.IsRemoved = true;

        repository.TryFindUserByEmail(email).Returns((true, user));
        securityManager.ComparePasswords("password", user.Salt, user.PasswordHash).Returns(true);

        var result = await service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Removed);
        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
        // Same line in the journal as for a ban: the password was proven first.
        await auditService.Received(1).LogAsync(user.UserId, SecurityEventType.LoginFailure,
            Arg.Any<string?>(), Arg.Any<string?>(), "Account removed");
    }

    [Fact]
    public async Task RefuseLoginForTheSystemActorEvenWhenItsPasswordMatches()
    {
        var email = "system@dm.local";
        var user = Create.User()
            .WithRole(UserRole.System)
            .WithCredentials("salt", "hash")
            .Please();

        repository.TryFindUserByEmail(email).Returns((true, user));
        // Everything a successful login needs is stubbed, a matching password
        // included: what stops the robot is the check on its role, and the refusal
        // has to hold with the credentials of a real account behind it.
        //
        // Any arguments, because the actor is deliberately hashed against the
        // decoy rather than against its own columns - the seed leaves those empty,
        // and the hash exists to spend the time, not to ask a question.
        securityManager
            .ComparePasswords(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        sessionFactory.Create(true, null).Returns(new CreateSession { Id = Guid.NewGuid() });
        repository.FindUserSettings(user.UserId).Returns(UserSettings.Default);
        repository.AddSession(user.UserId, Arg.Any<CreateSession>()).Returns(new Session { Id = Guid.NewGuid() });
        cryptoService.Encrypt(Arg.Any<string>()).Returns("encrypted-token");

        var result = await service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        // Answered as a wrong password on purpose: a distinct error would point at
        // the one account that exists but can never be logged into
        result.Error.Should().Be(AuthenticationError.WrongPassword);
        await repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());

        // And it pays for the refusal like every other refusal does. Short-circuited
        // on the role, this answer came back some hundred and fifty milliseconds
        // before any other and named the robot's address by that alone.
        securityManager.Received(1).ComparePasswords(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RefuseLoginWhenTheFullBanComesFromABanRow()
    {
        var email = "banned@example.com";
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);

        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.FullBan, now.AddHours(-1), now.AddHours(1))
        ];

        repository.TryFindUserByEmail(email).Returns((true, user));
        securityManager.ComparePasswords("password", user.Salt, user.PasswordHash).Returns(true);

        var result = await service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        // A ban that comes from a row refuses exactly like the flag does
        result.Error.Should().Be(AuthenticationError.Banned);
        // The day the ban runs out travels with the refusal, for the screen to
        // spell in the reader's own time zone.
        result.BanEndsUtc.Should().Be(now.AddHours(1));
    }
}
