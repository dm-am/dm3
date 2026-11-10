using System;
using OtpNet;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The arithmetic of RFC 6238, and nothing else.
/// </summary>
/// <remarks>
/// Everything a second factor is made of besides this - how the secret is
/// generated, where it is kept, how wide the window is, which steps have been
/// spent, what the URI says - is decided in this project. The library behind
/// this class answers one question: does the code match, and at which time
/// step.
///
/// The step number is what makes the class worth having. Without it a
/// verifier can only say yes or no, and "this exact code was already used" is
/// then unanswerable.
///
/// HMAC-SHA1, a thirty second step and six digits: most authenticator apps compute
/// with SHA-1 whatever the URI says, and HMAC does not lean on collision resistance.
/// </remarks>
internal static class TotpCalculator
{
    internal const int StepSeconds = 30;

    internal const int Digits = 6;

    /// <summary>
    /// The step the code matched at, or null if it matched none.
    /// </summary>
    /// <param name="secret">Shared secret, raw bytes</param>
    /// <param name="code">Code as the person typed it</param>
    /// <param name="moment">Moment to verify against</param>
    /// <param name="windowSteps">How many steps either side are accepted</param>
    public static long? Match(byte[] secret, string code, DateTimeOffset moment, int windowSteps)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var totp = new Totp(secret, StepSeconds, OtpHashMode.Sha1, Digits);
        var matched = totp.VerifyTotp(
            moment.UtcDateTime,
            code,
            out var step,
            new VerificationWindow(previous: windowSteps, future: windowSteps));

        return matched ? step : null;
    }

    public static string ToBase32(byte[] secret) => Base32Encoding.ToString(secret);

    public static byte[] FromBase32(string secret) => Base32Encoding.ToBytes(secret);
}
