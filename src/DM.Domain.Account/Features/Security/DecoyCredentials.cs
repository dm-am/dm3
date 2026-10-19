using System;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// The salt and hash a refusal is timed against when there is nothing real to
/// compare the password with.
/// </summary>
/// <remarks>
/// A missing account still pays for Argon2id, so its refusal takes as long as a
/// wrong password. The salt is well formed because the hasher decodes it.
/// </remarks>
internal static class DecoyCredentials
{
    /// <summary>Well formed salt that belongs to nobody</summary>
    public static readonly string Salt = Convert.ToBase64String(new byte[75]);

    /// <inheritdoc cref="Salt" />
    public static readonly string Hash = Convert.ToBase64String(new byte[32]);
}
