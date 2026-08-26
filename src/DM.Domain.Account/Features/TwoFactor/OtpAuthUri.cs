using System;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The string an authenticator app reads out of the QR code.
/// </summary>
/// <remarks>
/// The label is the username and not the address. An app writes the label into
/// its own list, and that list travels into a cloud backup for a great many
/// people; the mailbox has no business being there.
/// </remarks>
internal static class OtpAuthUri
{
    /// <summary>
    /// Builds the URI for one account.
    /// </summary>
    /// <param name="issuer">Name of the site, from configuration</param>
    /// <param name="username">Label the app files the entry under</param>
    /// <param name="base32Secret">The shared secret</param>
    public static string For(string issuer, string username, string base32Secret)
    {
        var escapedIssuer = Uri.EscapeDataString(issuer);
        var label = $"{escapedIssuer}:{Uri.EscapeDataString(username)}";

        // The algorithm is written out although most apps ignore it and assume
        // SHA-1 anyway. Stating what is actually computed costs nothing and
        // leaves the reader of the URI no room to guess.
        return $"otpauth://totp/{label}" +
               $"?secret={base32Secret}" +
               $"&issuer={escapedIssuer}" +
               $"&algorithm=SHA1" +
               $"&digits={TotpCalculator.Digits}" +
               $"&period={TotpCalculator.StepSeconds}";
    }
}
