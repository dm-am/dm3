namespace DM.Domain.Core.Identity;

/// <summary>
/// Version of the password hashing scheme currently in use.
/// </summary>
/// <remarks>
/// Written by every path that writes a hash, so a future scheme can be told apart.
/// </remarks>
public static class PasswordHashing
{
    /// <summary>
    /// Argon2id with the parameters in SecurityManager.
    /// </summary>
    public const int CurrentVersion = 4;
}
