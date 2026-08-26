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
/// The protocol has three steps and the factor is off between the first two.
/// That order is the point: everything that can go wrong with a setup - a clock
/// adrift, an app that saved nothing, the wrong QR scanned - is discovered at the
/// confirmation rather than days later at a sign-in where the only way in left is
/// recovery (INV-2).
/// </remarks>
public class TwoFactorServiceShould : UnitTestBase
{
    private static readonly Guid UserId = Guid.Parse("6c5b4a39-2817-4655-9403-1122334455aa");
    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Password = "correct horse";

    private readonly ITwoFactorRepository _repository;
    private readonly ITwoFactorVerifier _verifier;
    private readonly ISymmetricCryptoService _cryptoService;
    private readonly ISecurityManager _securityManager;
    private readonly IAuthenticationService _authenticationService;
    private readonly ISecurityAuditRepository _auditService;
    private readonly TwoFactorService _service;
    private readonly AuthenticatedUser _owner;

    public TwoFactorServiceShould()
    {
        _repository = Mock<ITwoFactorRepository>();
        _verifier = Mock<ITwoFactorVerifier>();
        _cryptoService = Mock<ISymmetricCryptoService>();
        _securityManager = Mock<ISecurityManager>();
        _authenticationService = Mock<IAuthenticationService>();
        _auditService = Mock<ISecurityAuditRepository>();
        var identityProvider = Mock<IIdentityProvider>();
        var dateTimeProvider = Mock<IDateTimeProvider>();

        _owner = Create.User(UserId)
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        _owner.Username = "reader";

        identityProvider.Current.Returns(
            Identity.Success(_owner, new Session { Id = Guid.NewGuid() }, UserSettings.Default, "token"));
        dateTimeProvider.Now.Returns(Moment);
        _securityManager.ComparePasswords(Password, "salt", "hash").Returns(true);
        _cryptoService.Encrypt(Arg.Any<string>())
            .Returns(call => Task.FromResult("envelope:" + (string)call[0]));

        _service = new TwoFactorService(
            _repository,
            _verifier,
            new TotpCalculator(),
            new RecoveryCodeFactory(),
            _cryptoService,
            _securityManager,
            identityProvider,
            _authenticationService,
            _auditService,
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

    // ── Issuing the secret ──

    /// <summary>AC-2: the answer carries the secret and the otpauth URI.</summary>
    [Fact]
    public async Task HandOverTheSecretAndTheUri()
    {
        var setup = await _service.IssueSecret(Password);

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
        _owner.Email = "reader@example.com";

        var setup = await _service.IssueSecret(Password);

        setup.OtpAuthUri.Should().NotContain("reader@example.com");
    }

    /// <summary>INV-3: what reaches storage is the envelope, not the secret.</summary>
    [Fact]
    public async Task StoreTheSecretOnlyInsideTheEnvelope()
    {
        var setup = await _service.IssueSecret(Password);

        await _cryptoService.Received(1).Encrypt(setup.Secret);
        await _repository.Received(1).IssueSecret(
            UserId, "envelope:" + setup.Secret, Moment, Arg.Any<CancellationToken>());
    }

    /// <summary>AC-3: without the password nothing is issued.</summary>
    [Fact]
    public async Task RefuseToIssueASecretWithoutTheCurrentPassword()
    {
        await Assert.ThrowsAsync<HttpBadRequestException>(() => _service.IssueSecret("wrong"));

        await _repository.DidNotReceive().IssueSecret(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>AC-4: a second call before confirmation replaces the secret.</summary>
    [Fact]
    public async Task ReplaceAnUnconfirmedSecretRatherThanRepeatIt()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment.AddMinutes(-5)));

        var first = await _service.IssueSecret(Password);
        var second = await _service.IssueSecret(Password);

        second.Secret.Should().NotBe(first.Secret,
            "an abandoned QR may have been read over a shoulder, so the live unconfirmed " +
            "secret is always the last one issued");
        await _repository.Received(2).IssueSecret(
            UserId, Arg.Any<string>(), Moment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseToIssueASecretWhenTheFactorIsAlreadyOn()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.IssueSecret(Password));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Confirming ──

    /// <summary>
    /// INV-2: only a code that matched the issued secret switches the factor on.
    /// </summary>
    [Fact]
    public async Task SwitchTheFactorOnOnlyForACodeThatMatched()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment.AddMinutes(-1)));
        _verifier.Accept(Arg.Any<TwoFactorState>(), "000000", null, Arg.Any<CancellationToken>())
            .Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Confirm("000000"));

        thrown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().Confirm(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<CancellationToken>());

        // AC-5: the attempt is counted. In the journal the owner reads, and not
        // in the lockout counter of the login - a person mistyping their very
        // first setup code must not be able to lock their own sign-in.
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorSetupFailure,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());

        // Under its own type. Written as a failed sign-in it was rendered to the
        // owner as "Неудачная попытка входа" - a sentence about an event that did
        // not happen, on the one screen he consults to find out whether it did.
        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.LoginFailure,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>AC-7: the recovery codes come back once, with the confirmation.</summary>
    [Fact]
    public async Task IssueTheRecoveryCodesWithTheConfirmation()
    {
        ArrangeConfirmation();

        var codes = await _service.Confirm("123456");

        codes.Codes.Should().HaveCount(10);

        // The set travels with the stamp, in the one call that carries both: it
        // is shown in this answer and never again, so it cannot be stored by a
        // second write that may not happen.
        await _repository.Received(1).Confirm(
            UserId, Moment, Arg.Is<IReadOnlyList<byte[]>>(hashes => hashes.Count == 10),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().ReplaceRecoveryCodes(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<byte[]>>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorEnabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>AC-8 and INV-16: confirming ends every other session.</summary>
    /// <remarks>
    /// And ends them before the factor goes on, which is the order that survives
    /// a failure. The other way round, a fault while ending the sessions leaves
    /// the factor already on with somebody else's year-long session inside it,
    /// and a second confirmation is refused as a factor already on - so nothing
    /// is left to retry. Ending them first costs a re-confirmation and leaves the
    /// account exactly as it was.
    /// </remarks>
    [Fact]
    public async Task EndEveryOtherSessionBeforeTheFactorGoesOn()
    {
        ArrangeConfirmation();

        await _service.Confirm("123456");

        Received.InOrder(() =>
        {
            _authenticationService.LogoutElsewhere();
            _repository.Confirm(
                UserId, Moment, Arg.Any<IReadOnlyList<byte[]>>(), Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// A failure at the write leaves the account off, not on and empty-handed.
    /// </summary>
    /// <remarks>
    /// The state this rules out is the expensive one: a factor switched on for an
    /// owner who never saw a recovery code. He would be tied to the one device
    /// holding the secret, and the mailed removal path - a week long by design -
    /// would be the whole of what he had left.
    /// </remarks>
    [Fact]
    public async Task LeaveTheFactorOffWhenTheConfirmationCannotBeStored()
    {
        ArrangeConfirmation();
        _repository.Confirm(UserId, Moment, Arg.Any<IReadOnlyList<byte[]>>(),
                Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("storage is down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.Confirm("123456"));

        // Nothing was written outside that one call, so there is no half of it to
        // survive: the transaction it runs in is the whole of the change.
        await _repository.DidNotReceive().ReplaceRecoveryCodes(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<byte[]>>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorEnabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>AC-6: past the setup window the confirmation is refused.</summary>
    [Fact]
    public async Task RefuseAConfirmationPastTheSetupWindow()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment.AddMinutes(-31)));
        _verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Confirm("123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Gone);
        await _repository.DidNotReceive().Confirm(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseAConfirmationWithNoSecretIssued()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns((TwoFactorState?)null);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Confirm("123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>AC-7, the other half: the set cannot be had a second time.</summary>
    [Fact]
    public async Task RefuseASecondConfirmationOfAFactorAlreadyOn()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Confirm("123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _repository.DidNotReceive().Confirm(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Two confirmations racing each other switch the factor on once.
    /// </summary>
    [Fact]
    public async Task RefuseAConfirmationThatLostTheRaceToSwitchOn()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment.AddMinutes(-1)));
        _verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);
        _repository.Confirm(UserId, Moment, Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<CancellationToken>()).Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Confirm("123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The set belongs to whichever confirmation switched the factor on, and
        // the loser is told nothing was switched on by it.
        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorEnabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    // ── Switching off and reissuing ──

    /// <summary>INV-18: switching off costs the password and a second factor.</summary>
    [Fact]
    public async Task RefuseToSwitchOffWithoutTheCurrentPassword()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());

        await Assert.ThrowsAsync<HttpBadRequestException>(() => _service.Disable("wrong", "123456"));

        await _repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseToSwitchOffWithoutASecondFactor()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        _verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Disable(Password, "000000"));

        thrown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchTheFactorOffWhenBothAreGiven()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        _verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);

        await _service.Disable(Password, "123456");

        await _repository.Received(1).Remove(UserId, Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorDisabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>AC-25 and INV-17: a reissue retires the previous set whole.</summary>
    [Fact]
    public async Task ReplaceTheWholeSetOfRecoveryCodes()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        _verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);

        var codes = await _service.ReissueRecoveryCodes(Password, "123456");

        codes.Codes.Should().HaveCount(10);
        // ReplaceRecoveryCodes is the only write, and it is a replacement rather
        // than an append: half old and half new means a code crossed off on paper
        // still opens the account.
        await _repository.Received(1).ReplaceRecoveryCodes(
            UserId, Arg.Is<IReadOnlyList<byte[]>>(hashes => hashes.Count == 10), Moment,
            Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRecoveryCodesReissued,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseToReissueCodesForAFactorThatIsNotOn()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns((TwoFactorState?)null);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => _service.ReissueRecoveryCodes(Password, "123456"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Status ──

    /// <summary>INV-1: "on" is the confirmation stamp and nothing else.</summary>
    [Fact]
    public async Task CallTheFactorOnExactlyWhenTheStampIsThere()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment));

        (await _service.GetStatus()).Enabled.Should().BeFalse();

        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());
        _repository.CountUnusedRecoveryCodes(UserId, Arg.Any<CancellationToken>()).Returns(7);

        var status = await _service.GetStatus();
        status.Enabled.Should().BeTrue();
        status.EnabledUtc.Should().Be(Moment.AddDays(-1));
        status.RecoveryCodesLeft.Should().Be(7);
    }

    /// <summary>INV-3: the status says nothing that could pass the factor.</summary>
    [Fact]
    public async Task SayNothingInTheStatusThatCouldPassTheFactor()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Confirmed());

        var status = await _service.GetStatus();

        status.ToString().Should().NotContain("envelope:secret");
        typeof(TwoFactorStatus).GetProperties()
            .Should().NotContain(property =>
                property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    private void ArrangeConfirmation()
    {
        _repository.Find(UserId, Arg.Any<CancellationToken>()).Returns(Unconfirmed(Moment.AddMinutes(-1)));
        _verifier.Accept(Arg.Any<TwoFactorState>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(true);
        _repository.Confirm(UserId, Moment, Arg.Any<IReadOnlyList<byte[]>>(),
            Arg.Any<CancellationToken>()).Returns(true);
    }
}
