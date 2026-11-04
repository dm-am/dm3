using System.Security.Cryptography;

namespace DM.Domain.Core.Tokens;

/// <summary>
/// The two halves of a confirmation link: the value the person receives and the
/// value the database keeps.
/// </summary>
/// <remarks>
/// A confirmation token is a credential, so only its hash is stored. Unsalted
/// SHA-256 suffices: the input is 122 bits of randomness, not a chosen password.
/// A Guid from IGuidFactory, so the seeding tool can make it deterministic.
/// </remarks>
public static class ConfirmationSecret
{
    /// <summary>
    /// What the database keeps for a secret handed to a person.
    /// </summary>
    /// <param name="secret">Value from the confirmation link</param>
    /// <returns>Hash to store and to look up by</returns>
    public static byte[] Hash(Guid secret) =>
        SHA256.HashData(secret.ToByteArray());
}
