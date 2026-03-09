using System;
using System.Security.Cryptography;

namespace DM.Domain.Account.Features.Security;

/// <inheritdoc />
internal class SaltFactory : ISaltFactory
{
    /// <inheritdoc />
    public string Create(int saltLength)
    {
        // Generate exactly the right number of random bytes
        // to produce a Base64 string of at least saltLength characters.
        // Every 3 bytes produce 4 Base64 characters.
        var byteCount = (int)Math.Ceiling(saltLength * 3.0 / 4.0);
        var buffer = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(buffer);
    }
}
