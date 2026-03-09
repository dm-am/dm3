using System;
using System.Security.Cryptography;

namespace DM.Domain.Account.Features.Security;

/// <inheritdoc />
internal class SecurityManager : ISecurityManager
{
    private readonly ISaltFactory _saltFactory;
    private readonly IHashProvider _hashProvider;

    /// <inheritdoc />
    public SecurityManager(
        ISaltFactory saltFactory,
        IHashProvider hashProvider)
    {
        _saltFactory = saltFactory;
        _hashProvider = hashProvider;
    }

    /// <inheritdoc />
    public (string Hash, string Salt) GeneratePassword(string password)
    {
        var salt = _saltFactory.Create(100);
        var hash = _hashProvider.ComputeHash(password, salt);
        return (Convert.ToBase64String(hash), salt);
    }

    /// <inheritdoc />
    public bool ComparePasswords(string password, string salt, string hash)
    {
        var computedHash = _hashProvider.ComputeHash(password, salt);
        var storedHash = Convert.FromBase64String(hash);

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }
}
