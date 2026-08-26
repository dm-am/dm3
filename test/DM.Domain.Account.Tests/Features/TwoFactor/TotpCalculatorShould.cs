using System;
using DM.Domain.Account.Features.TwoFactor;
using AwesomeAssertions;
using OtpNet;
using Xunit;

namespace DM.Domain.Account.Tests.Features.TwoFactor;

/// <summary>
/// The arithmetic of RFC 6238, and the window it is read through.
/// </summary>
/// <remarks>
/// AC-12: a code one step either side is accepted, a code two steps away is not.
/// Three codes out of a million give one attempt a chance of three in a million,
/// which is nothing beside the attempt limits; a wider window starts to matter.
/// </remarks>
public class TotpCalculatorShould
{
    private static readonly byte[] Secret = Base32Encoding.ToBytes("JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP");
    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TotpCalculator _calculator = new();

    private static string CodeAt(DateTimeOffset moment) =>
        new Totp(Secret, TotpCalculator.StepSeconds, OtpHashMode.Sha1, TotpCalculator.Digits)
            .ComputeTotp(moment.UtcDateTime);

    [Fact]
    public void AcceptTheCodeOfTheCurrentStep() =>
        _calculator.Match(Secret, CodeAt(Moment), Moment, windowSteps: 1)
            .Should().NotBeNull();

    [Theory]
    [InlineData(-30)]
    [InlineData(30)]
    public void AcceptACodeOneStepEitherSide(int offsetSeconds) =>
        _calculator
            .Match(Secret, CodeAt(Moment.AddSeconds(offsetSeconds)), Moment, windowSteps: 1)
            .Should().NotBeNull(
                "a step back is for a slow connection and slow typing, a step forward for a " +
                "device whose clock has run ahead");

    [Theory]
    [InlineData(-60)]
    [InlineData(60)]
    public void RefuseACodeTwoStepsAway(int offsetSeconds) =>
        _calculator
            .Match(Secret, CodeAt(Moment.AddSeconds(offsetSeconds)), Moment, windowSteps: 1)
            .Should().BeNull("the window is one step either side and nothing wider");

    /// <summary>
    /// The step number is the reason this interface exists at all.
    /// </summary>
    /// <remarks>
    /// Without it a verifier can only say yes or no, and "this exact code has
    /// already been used" becomes unanswerable - which is the whole of the replay
    /// guard (INV-6).
    /// </remarks>
    [Fact]
    public void ReturnTheStepTheCodeWasComputedFor()
    {
        var expected = Moment.ToUnixTimeSeconds() / TotpCalculator.StepSeconds;

        _calculator.Match(Secret, CodeAt(Moment), Moment, windowSteps: 1)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("000000")]
    [InlineData("not a code")]
    public void RefuseAnythingThatIsNotTheCode(string value) =>
        _calculator.Match(Secret, value, Moment, windowSteps: 1).Should().BeNull();

    /// <summary>
    /// The form the secret is stored and shown in survives a round trip.
    /// </summary>
    /// <remarks>
    /// Base32 is the one thing the library is taken for besides the step number:
    /// the BCL has no encoder for it, and an authenticator app takes nothing else.
    /// </remarks>
    [Fact]
    public void CarryTheSecretThroughBase32AndBack() =>
        _calculator.FromBase32(_calculator.ToBase32(Secret)).Should().Equal(Secret);
}
