using System;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// The salt and hash a refusal is timed against when there is nothing real to
/// compare the password with.
/// </summary>
/// <remarks>
/// Two refusals required to be indistinguishable are indistinguishable in text
/// on every surface that has one: both come back as "wrong email or password".
/// In time they were not. A missing account skipped Argon2id entirely and
/// answered tens of milliseconds sooner, which says the address carries no
/// account without any password having been tried.
///
/// The salt has to be well formed because the hasher decodes it; neither value
/// is ever stored or compared against anything real. They live here rather than
/// inside the login because the password now stands in for a session on a second
/// surface, the request to bring a removed account back, and a second copy of
/// this pair is a second chance to get one of them wrong.
/// </remarks>
internal static class DecoyCredentials
{
    /// <summary>Well formed salt that belongs to nobody</summary>
    public static readonly string Salt = Convert.ToBase64String(new byte[75]);

    /// <inheritdoc cref="Salt" />
    public static readonly string Hash = Convert.ToBase64String(new byte[32]);
}
