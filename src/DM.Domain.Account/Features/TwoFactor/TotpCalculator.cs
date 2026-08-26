using System;
using OtpNet;

namespace DM.Domain.Account.Features.TwoFactor;

/// <inheritdoc />
/// <remarks>
/// HMAC-SHA1, a thirty second step and six digits. SHA-1 here is not a choice
/// of a weak hash but a requirement of compatibility: most authenticator apps
/// ignore the algorithm parameter of an otpauth URI and compute with SHA-1
/// anyway, so a URI announcing SHA-256 silently produces codes that never match
/// for part of the user base. Collision resistance is not what HMAC leans on,
/// and the known weaknesses of SHA-1 do not reach this construction.
/// </remarks>
internal class TotpCalculator : ITotpCalculator
{
    /// <summary>Seconds one code is valid for.</summary>
    internal const int StepSeconds = 30;

    /// <summary>Digits a code is made of.</summary>
    internal const int Digits = 6;

    /// <inheritdoc />
    public long? Match(byte[] secret, string code, DateTimeOffset moment, int windowSteps)
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

    /// <inheritdoc />
    public string ToBase32(byte[] secret) => Base32Encoding.ToString(secret);

    /// <inheritdoc />
    public byte[] FromBase32(string secret) => Base32Encoding.ToBytes(secret);
}
