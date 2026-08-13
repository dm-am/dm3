using System;
using System.Security.Cryptography;

namespace DM.Domain.Core.Tokens;

/// <summary>
/// The two halves of a confirmation link: the value the person receives and the
/// value the database keeps.
/// </summary>
/// <remarks>
/// They used to be the same value. A password reset, an activation and an email
/// change all stored in Tokens.TokenId exactly what the letter carried, so one
/// read of that table — a dump, a backup, a copy restored for debugging, an open
/// console — handed over every account with an unspent token, without a password
/// and without access to the mailbox.
///
/// A confirmation token is a credential, and a credential is stored the way a
/// password is: only its hash. The secret exists in one place, the letter, and in
/// one moment, the request that redeems it. SHA-256 without a salt is the right
/// primitive here and Argon2 is not: the input is 122 bits of our own randomness
/// rather than a human-chosen password, so there is nothing to slow down a guess
/// of and nothing to rainbow-table.
///
/// The secret stays a Guid rather than becoming wider random bytes on purpose:
/// identifiers in this project come from IGuidFactory, which the seeding tool
/// replaces with a deterministic one so a fixture built twice is the same
/// fixture. A second source of randomness would step outside that.
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
