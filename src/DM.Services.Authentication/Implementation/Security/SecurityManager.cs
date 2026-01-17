using System;
using System.Text;

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
    public (string Hash, string Salt) GeneratePassword(string password)
    {
        var salt = _saltFactory.Create(100);
        var hash = _hashProvider.ComputeSha256(password, salt);
        return (Convert.ToBase64String(hash), salt);
    }

    /// <inheritdoc />
    public bool ComparePasswords(string password, string salt, string hash)
    {
        var saltedHash = _hashProvider.ComputeSha256(password, salt);
        var passwordHashByteArray = Convert.FromBase64String(hash);
        return string.Equals(
            Encoding.UTF8.GetString(saltedHash),
            Encoding.UTF8.GetString(passwordHashByteArray));
    }
}