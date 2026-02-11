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
        // Version 3 is PBKDF2-SHA256 with 600K iterations (current standard)
        // Versions 1-2 are legacy - kept for migration but DB is empty so not needed
        var computedHash = version switch
        {
            3 => _hashProvider.ComputePbkdf2(password, salt),
            _ => throw new ArgumentException($"Unknown password hash version: {version}. Only version 3 (PBKDF2) is supported.", nameof(version))
        };

        var storedHash = Convert.FromBase64String(hash);

        // Use constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }

    /// <inheritdoc />
    public bool NeedsRehash(int version) => version < _hashProvider.CurrentVersion;
}
