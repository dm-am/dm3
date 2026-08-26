using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OtpNet;
using Xunit;

namespace DM.Domain.Account.Tests.Features.TwoFactor;

/// <summary>
/// The one place a presented second factor is judged.
/// </summary>
/// <remarks>
/// Covers the two replay guards, which are the substance of the feature:
/// INV-6, a time step is good for one use across every session and every
/// challenge of the account, and INV-5, a recovery code is spent exactly once
/// because spending it is a conditional update over one row.
/// </remarks>
public class TwoFactorVerifierShould : UnitTestBase
{
    private static readonly Guid UserId = Guid.Parse("2f1c0d3e-4b5a-4c6d-8e7f-90a1b2c3d4e5");
    private static readonly string Base32Secret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ITwoFactorRepository _repository;
    private readonly ISecurityAuditRepository _auditService;
    private readonly TwoFactorVerifier _verifier;
    private readonly RecoveryCodeFactory _recoveryCodes = new();

    public TwoFactorVerifierShould()
    {
        _repository = Mock<ITwoFactorRepository>();
        _auditService = Mock<ISecurityAuditRepository>();
        var cryptoService = Mock<ISymmetricCryptoService>();
        var dateTimeProvider = Mock<IDateTimeProvider>();

        // The envelope is somebody else's business here: what the verifier needs
        // is the secret the envelope was carrying.
        cryptoService.Decrypt(Arg.Any<string>()).Returns(call => Task.FromResult((string)call[0]));
        dateTimeProvider.Now.Returns(Moment);

        _verifier = new TwoFactorVerifier(
            _repository,
            new TotpCalculator(),
            _recoveryCodes,
            cryptoService,
            _auditService,
            dateTimeProvider,
            Options.Create(new TwoFactorConfiguration { VerificationWindowSteps = 1 }));
    }

    private static TwoFactorState State() => new()
    {
        UserId = UserId,
        Secret = Base32Secret,
        CreatedUtc = Moment.AddMinutes(-1),
        ConfirmedUtc = Moment.AddMinutes(-1)
    };

    private static string CodeAt(DateTimeOffset moment) =>
        new Totp(Base32Encoding.ToBytes(Base32Secret), TotpCalculator.StepSeconds,
                OtpHashMode.Sha1, TotpCalculator.Digits)
            .ComputeTotp(moment.UtcDateTime);

    private static long StepAt(DateTimeOffset moment) =>
        moment.ToUnixTimeSeconds() / TotpCalculator.StepSeconds;

    [Fact]
    public async Task AcceptACodeFromTheDevice()
    {
        _repository.TryAcceptStep(UserId, StepAt(Moment), Moment).Returns(true);

        (await _verifier.Accept(State(), CodeAt(Moment), null)).Should().BeTrue();
    }

    [Fact]
    public async Task RefuseACodeThatMatchesNothing()
    {
        (await _verifier.Accept(State(), "000000", null)).Should().BeFalse();

        // Nothing is spent by a refusal: a wrong code must not be able to burn a
        // step or a recovery code on the owner's behalf.
        await _repository.DidNotReceive().TryAcceptStep(
            Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// INV-6: the same code twice is refused the second time.
    /// </summary>
    /// <remarks>
    /// The refusal is the storage answering "the recorded step is not smaller
    /// than this one", which is what makes it hold across sessions and across
    /// challenges rather than inside one request.
    /// </remarks>
    [Fact]
    public async Task RefuseACodeWhoseStepWasAlreadyAccepted()
    {
        var step = StepAt(Moment);
        _repository.TryAcceptStep(UserId, step, Moment).Returns(true, false);
        var code = CodeAt(Moment);

        (await _verifier.Accept(State(), code, null)).Should().BeTrue();
        (await _verifier.Accept(State(), code, null)).Should().BeFalse(
            "a code seen over a shoulder is arithmetically valid for up to ninety seconds, " +
            "and the step it belongs to is what stops it");
    }

    /// <summary>
    /// AC-16, the code half: two requests carrying one code give one success.
    /// </summary>
    [Fact]
    public async Task LetOnlyOneOfTwoSimultaneousCodesWin()
    {
        var step = StepAt(Moment);
        // The conditional update is the arbiter: exactly one caller changes a row.
        _repository.TryAcceptStep(UserId, step, Moment).Returns(true, false);
        var code = CodeAt(Moment);

        var results = await Task.WhenAll(
            _verifier.Accept(State(), code, null),
            _verifier.Accept(State(), code, null));

        results.Should().ContainSingle(accepted => accepted);
    }

    [Fact]
    public async Task AcceptARecoveryCodeAndSpendIt()
    {
        var code = _recoveryCodes.Create(1)[0];
        var codeId = Guid.NewGuid();
        _repository.GetUnusedRecoveryCodes(UserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<(Guid, byte[])>>([(codeId, _recoveryCodes.Hash(code))]);
        _repository.TrySpendRecoveryCode(codeId, Moment, "1.2.3.4").Returns(true);

        (await _verifier.Accept(State(), code, "1.2.3.4")).Should().BeTrue();

        await _repository.Received(1).TrySpendRecoveryCode(
            codeId, Moment, "1.2.3.4", Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRecoveryCodeUsed, "1.2.3.4",
            Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>
    /// Section 5: every spend says how many codes are left.
    /// </summary>
    /// <remarks>
    /// The count is taken after the spend, so the entry names what is left
    /// rather than what was left. In the entry and not only in the status
    /// endpoint, because the line the owner is reading is the one that tells him
    /// how close he is to having nothing on paper at all.
    /// </remarks>
    [Fact]
    public async Task SayHowManyRecoveryCodesAreLeftInTheJournalEntry()
    {
        var code = _recoveryCodes.Create(1)[0];
        var codeId = Guid.NewGuid();
        _repository.GetUnusedRecoveryCodes(UserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<(Guid, byte[])>>([(codeId, _recoveryCodes.Hash(code))]);
        _repository.TrySpendRecoveryCode(codeId, Moment, "1.2.3.4").Returns(true);
        _repository.CountUnusedRecoveryCodes(UserId, Arg.Any<CancellationToken>()).Returns(7);

        (await _verifier.Accept(State(), code, "1.2.3.4")).Should().BeTrue();

        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRecoveryCodeUsed, "1.2.3.4",
            Arg.Any<string?>(), "Осталось резервных кодов: 7");
    }

    /// <summary>
    /// INV-5 and AC-15: a code already spent is not in the set and is refused.
    /// </summary>
    [Fact]
    public async Task RefuseARecoveryCodeThatIsNoLongerUnspent()
    {
        var code = _recoveryCodes.Create(1)[0];
        _repository.GetUnusedRecoveryCodes(UserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<(Guid, byte[])>>([]);

        (await _verifier.Accept(State(), code, null)).Should().BeFalse();
    }

    /// <summary>
    /// AC-16, the recovery half: the conditional update decides, not the read.
    /// </summary>
    [Fact]
    public async Task RefuseARecoveryCodeThatAnotherRequestSpentFirst()
    {
        var code = _recoveryCodes.Create(1)[0];
        var codeId = Guid.NewGuid();
        _repository.GetUnusedRecoveryCodes(UserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<(Guid, byte[])>>([(codeId, _recoveryCodes.Hash(code))]);
        // Both callers read the code as unspent; only one of them changes a row.
        _repository.TrySpendRecoveryCode(
            codeId, Arg.Any<DateTimeOffset>(), Arg.Any<string?>()).Returns(false);

        (await _verifier.Accept(State(), code, null)).Should().BeFalse();
        await _repository.DidNotReceive().MarkVerified(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseARecoveryCodeBelongingToSomebodyElsesSet()
    {
        var mine = _recoveryCodes.Create(1)[0];
        var theirs = _recoveryCodes.Create(1)[0];
        _repository.GetUnusedRecoveryCodes(UserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<(Guid, byte[])>>([(Guid.NewGuid(), _recoveryCodes.Hash(mine))]);

        (await _verifier.Accept(State(), theirs, null)).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public async Task RefuseAnEmptyValueWithoutAskingStorage(string value)
    {
        (await _verifier.Accept(State(), value, null)).Should().BeFalse();

        await _repository.DidNotReceive().GetUnusedRecoveryCodes(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
