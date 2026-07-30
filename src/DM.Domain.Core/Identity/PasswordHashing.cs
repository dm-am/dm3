namespace DM.Domain.Core.Identity;

/// <summary>
/// Version of the password hashing scheme currently in use.
/// </summary>
/// <remarks>
/// Stored alongside every hash so that a future parameter change can be told
/// apart from the current one and upgraded on successful login. There is no
/// dispatcher yet — there is nothing to dispatch to, because no legacy scheme
/// exists in this codebase — but the number has to be written by every path that
/// writes a hash. A password change that updates the hash and leaves the version
/// behind is exactly the row that a later dispatcher would decode with the wrong
/// algorithm and lock out permanently.
/// </remarks>
public static class PasswordHashing
{
    /// <summary>
    /// Argon2id with the parameters in HashProvider.
    /// </summary>
    public const int CurrentVersion = 4;
}
