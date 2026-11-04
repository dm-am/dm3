using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Identity = DM.Domain.Account.Features.Authentication.Identity;

namespace DM.Domain.Account.Tests.Features.TwoFactor;

/// <summary>
/// The second factor as its owner switches it on and off.
/// </summary>
/// <remarks>
/// The factor is off until confirmed, so a broken setup shows at the confirmation, not at a sign-in.
/// </remarks>
public class TwoFactorServiceShould : UnitTestBase
{
    private static readonly Guid UserId = Guid.Parse("6c5b4a39-2817-4655-9403-1122334455aa");
    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>When the secret a confirmation is checked against was issued.</summary>
    private static readonly DateTimeOffset SecretIssued = Moment.AddMinutes(-1);
    private const string Password = "correct horse";

    private readonly ITwoFactorRepository repository;
    private readonly ITwoFactorVerifier verifier;
    private readonly ISymmetricCryptoService cryptoService;
    private readonly ISecurityManager securityManager;
    private readonly ISessionService sessionService;
    private readonly ISecurityAuditRepository auditService;
    private readonly TwoFactorService service;
    private readonly AuthenticatedUser owner;

    public TwoFactorServiceShould()
    {
        repository = Mock<ITwoFactorRepository>();
        verifier = Mock<ITwoFactorVerifier>();
        cryptoService = Mock<ISymmetricCryptoService>();
        securityManager = Mock<ISecurityManager>();
        sessionService = Mock<ISessionService>();
        auditService = Mock<ISecurityAuditRepository>();
        var identityProvider = Mock<IIdentityProvider>();
        var dateTimeProvider = Mock<IDateTimeProvider>();

        owner = Create.User(UserId)
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        owner.Username = "reader";

        identityProvider.Current.Returns(
            Identity.Success(owner, new Session { Id = Guid.NewGuid() }, UserSettings.Default, "token"));
        dateTimeProvider.Now.Returns(Moment);
        securityManager.ComparePasswords(Password, "salt", "hash").Returns(true);
        cryptoService.Encrypt(Arg.Any<string>())
            .Returns(call => Task.FromResult("envelope:" + (string)call[0]));
        // The write is what decides whether the secret was taken, so the default
        // of a substitute - false - would refuse every setup in this file. The
        // reissue answers the same way, and for the same reason: the factor it
        // issues a set for can be gone by the time the set is written.
        repository.IssueSecret(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>()).Returns(true);
        repository.ReplaceRecoveryCodes(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(true);

        service = new TwoFactorService(
            repository,
            verifier,
            cryptoService,
            securityManager,
            identityProvider,
            sessionService,
            auditService,
            dateTimeProvider,
            Options.Create(new TwoFactorConfiguration
            {
                Issuer = "Dungeon Master",
                SetupWindowMinutes = 30,
                RecoveryCodeCount = 10
            }));
    }

    private static TwoFactorState Unconfirmed(DateTimeOffset createdUtc) => new()
    {
        UserId = UserId,
        Secret = "envelope:secret",
        CreatedUtc = createdUtc
    };

    private static TwoFactorState Confirmed() => new()
    {
        UserId = UserId,
        Secret = "envelope:secret",
        CreatedUtc = Moment.AddDays(-1),
        ConfirmedUtc = Moment.AddDays(-1)
    };

    /// <summary>The answer carries the secret and the otpauth URI.</summary>
    [Fact]
    public async Task HandOverTheSecretAndTheUri()
    {
        var setup = await service.IssueSecret(Password);

        setup.Secret.Should().NotBeNullOrWhiteSpace();
        setup.OtpAuthUri.Should().StartWith("otpauth://totp/Dungeon%20Master:reader?secret=" + setup.Secret);
        setup.OtpAuthUri.Should().Contain("issuer=Dungeon%20Master");
    }

    /// <summary>
    /// The label in the URI is the username, never the address.
    /// </summary>
    /// <remarks>
    /// An authenticator writes the label into its own list, and that list travels
    /// into a cloud backup for a great many people.
    /// </remarks>
    [Fact]
    public async Task LabelTheUriWithTheNameAndNotTheAddress()
    {
        owner.Email = "reader@example.com";

        var setup = await service.IssueSecret(Password);

        setup.OtpAuthUri.Should().NotContain("reader@example.com");
    }

    /// <summary>What reaches storage is the envelope, not the secret.</summary>
    [Fact]
    public async Task StoreTheSecretOnlyInsideTheEnvelope()
    {
        var setup = await service.IssueSecret(Password);

        await cryptoService.Received(1).Encrypt(setup.Secret);
        await repository.Received(1).IssueSecret(
            UserId, "envelope:" + setup.Secret, Moment, Arg.Any<CancellationToken>());
    }

    /// <summary>Without the password nothing is issued.</summary>
    [Fact]
    public async Task RefuseToIssueASecretWithoutTheCurrentPassword()
    {
        await Assert.ThrowsAsync<HttpBadRequestException>(() => service.IssueSecret("wrong"));

        await repository.DidNotReceive().IssueSecret(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A second call before confirmation replaces the secret.</summary>
    [Fact]
    public async Task ReplaceAnUnconfirmedSecretRatherThanRepeatIt()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment.AddMinutes(-5)));

        var first = await service.IssueSecret(Password);
        var second = await service.IssueSecret(Password);

        second.Secret.Should().NotBe(first.Secret,
            "an abandoned QR may have been read over a shoulder, so the live unconfirmed " +
            "secret is always the last one issued");
        await repository.Received(2).IssueSecret(
            UserId, Arg.Any<string>(), Moment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseToIssueASecretWhenTheFactorIsAlreadyOn()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.IssueSecret(Password));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Only a code that matched the issued secret switches the factor on.
    /// </summary>
    [Fact]
    public async Task SwitchTheFactorOnOnlyForACodeThatMatched()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(SecretIssued));
        verifier.Accept(Arg.Any<TwoFactorState>(), "000000", null, Arg.Any<CancellationToken>())
            .Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Confirm("000000"));

        thrown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await repository.DidNotReceiveWithAnyArgs().Confirm(default, default, default, default!, default);

        // The attempt is counted. In the journal the owner reads, and not
        // in the lockout counter of the login - a person mistyping their very
        // first setup code must not be able to lock their own sign-in.
        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorSetupFailure,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());

        // Under its own type. Written as a failed sign-in it was rendered to the
        // owner as "Неудачная попытка входа" - a sentence about an event that did
        // not happen, on the one screen he consults to find out whether it did.
        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.LoginFailure,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>The recovery codes come back once, with the confirmation.</summary>
    [Fact]
    public async Task IssueTheRecoveryCodesWithTheConfirmation()
    {
        ArrangeConfirmation();

        var codes = await service.Confirm("123456");

        codes.Codes.Should().HaveCount(10);

        // The set travels with the stamp, in the one call that carries both: it
        // is shown in this answer and never again, so it cannot be stored by a
        // second write that may not happen.
        await repository.Received(1).Confirm(
            UserId, SecretIssued, Moment, Arg.Is<IReadOnlyList<byte[]>>(hashes => hashes.Count == 10),
            Arg.Any<CancellationToken>());
        await repository.DidNotReceive().ReplaceRecoveryCodes(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<byte[]>>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorEnabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>Confirming ends every other session.</summary>
    /// <remarks>
    /// Before the factor goes on, which is the order that survives a failure.
    /// </remarks>
    [Fact]
    public async Task EndEveryOtherSessionBeforeTheFactorGoesOn()
    {
        ArrangeConfirmation();

        await service.Confirm("123456");

        Received.InOrder(() =>
        {
            sessionService.LogoutElsewhere();
            repository.Confirm(
                UserId, SecretIssued, Moment, Arg.Any<IReadOnlyList<byte[]>>(),
                Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// A failure at the write leaves the account off, not on and empty-handed.
    /// </summary>
    /// <remarks>
    /// A factor switched on for an owner who never saw a recovery code ties him to one device.
    /// </remarks>
    [Fact]
    public async Task LeaveTheFactorOffWhenTheConfirmationCannotBeStored()
    {
        ArrangeConfirmation();
        repository.Confirm(UserId, SecretIssued, Moment, Arg.Any<IReadOnlyList<byte[]>>(),
                Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("storage is down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Confirm("123456"));

        // Nothing was written outside that one call, so there is no half of it to
        // survive: the transaction it runs in is the whole of the change.
        await repository.DidNotReceive().ReplaceRecoveryCodes(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<byte[]>>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorEnabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseAConfirmationPastTheSetupWindow()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment.AddMinutes(-31)));
        verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Confirm("123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Gone);
        await repository.DidNotReceiveWithAnyArgs().Confirm(default, default, default, default!, default);
    }

    [Fact]
    public async Task RefuseAConfirmationWithNoSecretIssued()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns((TwoFactorState?)null);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Confirm("123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>A factor already on cannot be confirmed a second time.</summary>
    [Fact]
    public async Task RefuseASecondConfirmationOfAFactorAlreadyOn()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Confirm("123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await repository.DidNotReceiveWithAnyArgs().Confirm(default, default, default, default!, default);
    }

    /// <summary>
    /// The confirmation names the secret it checked, so a setup started
    /// elsewhere cannot be switched on by somebody else's code.
    /// </summary>
    /// <remarks>
    /// A second device asking for a QR replaces the secret in place between the read and the stamp.
    /// </remarks>
    [Fact]
    public async Task NameTheSecretItCheckedWhenSwitchingTheFactorOn()
    {
        ArrangeConfirmation();

        await service.Confirm("123456");

        await repository.Received(1).Confirm(
            UserId, SecretIssued, Moment, Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The secret gone from under the confirmation is refused, and refused the
    /// way a code that does not match is.
    /// </summary>
    /// <remarks>
    /// The code belongs to a secret the account no longer holds.
    /// </remarks>
    [Fact]
    public async Task RefuseAConfirmationOfASecretThatIsNoLongerThere()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(SecretIssued));
        verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);
        repository.Confirm(UserId, SecretIssued, Moment, Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<CancellationToken>()).Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Confirm("123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The set belongs to whichever confirmation switched the factor on, and
        // the loser is told nothing was switched on by it.
        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorEnabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>
    /// A setup that lost the race to a confirmation is refused by the write.
    /// </summary>
    /// <remarks>
    /// The read above it answered "off" and a confirmation committed between the
    /// two. Same refusal as the read gives, because what the owner is told must
    /// not depend on which of the two noticed.
    /// </remarks>
    [Fact]
    public async Task RefuseASetupThatLostTheRaceToAConfirmation()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(SecretIssued));
        repository.IssueSecret(UserId, Arg.Any<string>(), Moment, Arg.Any<CancellationToken>())
            .Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.IssueSecret(Password));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>Switching off costs the password and a second factor.</summary>
    [Fact]
    public async Task RefuseToSwitchOffWithoutTheCurrentPassword()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());

        await Assert.ThrowsAsync<HttpBadRequestException>(() => service.Disable("wrong", "123456"));

        await repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseToSwitchOffWithoutASecondFactor()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Disable(Password, "000000"));

        thrown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchTheFactorOffWhenBothAreGiven()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);

        await service.Disable(Password, "123456");

        await repository.Received(1).Remove(UserId, Arg.Any<CancellationToken>());
        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorDisabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>A reissue retires the previous set whole.</summary>
    [Fact]
    public async Task ReplaceTheWholeSetOfRecoveryCodes()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);

        var codes = await service.ReissueRecoveryCodes(Password, "123456");

        codes.Codes.Should().HaveCount(10);
        // ReplaceRecoveryCodes is the only write, and it is a replacement rather
        // than an append: half old and half new means a code crossed off on paper
        // still opens the account.
        await repository.Received(1).ReplaceRecoveryCodes(
            UserId, Arg.Is<IReadOnlyList<byte[]>>(hashes => hashes.Count == 10), Moment,
            Arg.Any<CancellationToken>());
        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRecoveryCodesReissued,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseToReissueCodesForAFactorThatIsNotOn()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns((TwoFactorState?)null);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => service.ReissueRecoveryCodes(Password, "123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// A factor taken off while the reissue was asked for hands out no codes.
    /// </summary>
    /// <remarks>
    /// The state is read before the password and the code are checked, and a mailed removal may come due in between.
    /// </remarks>
    [Fact]
    public async Task RefuseToReissueCodesForAFactorTakenOffWhileItWasAsked()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);
        repository.ReplaceRecoveryCodes(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => service.ReissueRecoveryCodes(Password, "123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
        thrown.Message.Should().Be(RefusalMessage.TwoFactorNotEnabled);
        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorRecoveryCodesReissued,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>The status "on" is the confirmation stamp and nothing else.</summary>
    [Fact]
    public async Task CallTheFactorOnExactlyWhenTheStampIsThere()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment));

        (await service.GetStatus()).Enabled.Should().BeFalse();

        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        repository.CountUnusedRecoveryCodes(UserId, Arg.Any<CancellationToken>()).Returns(7);

        var status = await service.GetStatus();
        status.Enabled.Should().BeTrue();
        status.EnabledUtc.Should().Be(Moment.AddDays(-1));
        status.RecoveryCodesLeft.Should().Be(7);
    }

    [Fact]
    public async Task SayNothingInTheStatusThatCouldPassTheFactor()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());

        var status = await service.GetStatus();

        status.ToString().Should().NotContain("envelope:secret");
        typeof(TwoFactorStatus).GetProperties()
            .Should().NotContain(property =>
                property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    private void ArrangeConfirmation()
    {
        repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(SecretIssued));
        verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);
        repository.Confirm(UserId, SecretIssued, Moment, Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<CancellationToken>()).Returns(true);
    }
}
