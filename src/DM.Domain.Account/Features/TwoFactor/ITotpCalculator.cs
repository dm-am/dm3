using System;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The arithmetic of RFC 6238, and nothing else.
/// </summary>
/// <remarks>
/// Everything a second factor is made of besides this - how the secret is
/// generated, where it is kept, how wide the window is, which steps have been
/// spent, what the URI says - is decided in this project. The library behind
/// this interface answers one question: does the code match, and at which time
/// step.
///
/// The step number is what makes the interface worth having. Without it a
/// verifier can only say yes or no, and "this exact code was already used" is
/// then unanswerable.
/// </remarks>
public interface ITotpCalculator
{
    /// <summary>
    /// The step the code matched at, or null if it matched none.
    /// </summary>
    /// <param name="secret">Shared secret, raw bytes</param>
    /// <param name="code">Code as the person typed it</param>
    /// <param name="moment">Moment to verify against</param>
    /// <param name="windowSteps">How many steps either side are accepted</param>
    long? Match(byte[] secret, string code, DateTimeOffset moment, int windowSteps);

    /// <summary>
    /// The secret as an authenticator app expects to receive it.
    /// </summary>
    /// <param name="secret">Shared secret, raw bytes</param>
    string ToBase32(byte[] secret);

    /// <summary>
    /// The secret back from the form it is stored and shown in.
    /// </summary>
    /// <param name="secret">Base32 secret</param>
    byte[] FromBase32(string secret);
}
