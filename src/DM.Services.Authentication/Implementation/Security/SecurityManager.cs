using System;
using System.Security.Cryptography;

namespace DM.Services.Authentication.Implementation.Security;

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
    public (string Hash, string Salt, int Version) GeneratePassword(string password)
    {
        var salt = _saltFactory.Create(100);
        var hash = _hashProvider.ComputePbkdf2(password, salt);
        return (Convert.ToBase64String(hash), salt, _hashProvider.CurrentVersion);
    }

    /// <inheritdoc />
    public bool ComparePasswords(string password, string salt, string hash, int version)
    {
        var computedHash = version switch
        {
            1 => _hashProvider.ComputeSha256(password, salt),
            2 => _hashProvider.ComputePbkdf2(password, salt),
            _ => throw new ArgumentException($"Unknown password hash version: {version}", nameof(version))
        };

        var storedHash = Convert.FromBase64String(hash);

        // Use constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }

    /// <inheritdoc />
    public bool NeedsRehash(int version) => version < _hashProvider.CurrentVersion;
}
