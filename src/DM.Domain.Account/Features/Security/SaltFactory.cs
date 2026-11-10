using System;
using System.Security.Cryptography;

namespace DM.Domain.Account.Features.Security;

internal static class SaltFactory
{
    public static string Create(int saltLength)
    {
        // Every 3 bytes produce 4 Base64 characters.
        var byteCount = (int)Math.Ceiling(saltLength * 3.0 / 4.0);
        var buffer = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(buffer);
    }
}
