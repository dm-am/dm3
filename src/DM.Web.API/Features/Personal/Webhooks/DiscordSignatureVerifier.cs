using System;
using System.Globalization;
using System.Text;
using DM.Domain.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace DM.Web.API.Features.Personal.Webhooks;

/// <summary>
/// Discord's proof that an interaction is its own: an Ed25519 signature over
/// timestamp + raw body, made moments ago.
/// </summary>
public class DiscordSignatureVerifier
{
    /// <summary>How far a signed timestamp may stand from the server's clock, either way.</summary>
    /// <remarks>
    /// Discord signs as it sends and waits seconds for the answer, so the width is room for clock
    /// skew alone: past it a signed interaction is a captured one sent again.
    /// </remarks>
    private static readonly TimeSpan TimestampWindow = TimeSpan.FromMinutes(5);

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DiscordSignatureVerifier> _logger;

    public DiscordSignatureVerifier(IDateTimeProvider dateTimeProvider, ILogger<DiscordSignatureVerifier> logger)
    {
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public bool Verify(string publicKeyHex, string signatureHex, string timestamp, byte[] body)
    {
        if (!IsSigned(publicKeyHex, signatureHex, timestamp, body))
        {
            _logger.LogWarning("Invalid Discord interaction signature");
            return false;
        }

        // After the signature, so the log tells a replay or a drifted clock from a
        // forgery; the caller is told the same either way.
        if (!IsWithinWindow(timestamp))
        {
            _logger.LogWarning(
                "Discord interaction with a valid signature and a timestamp outside the window: {Timestamp}",
                timestamp);
            return false;
        }

        return true;
    }

    /// <remarks>
    /// The fixed-time rule WebhookController follows for the Telegram secret
    /// holds here by construction: Ed25519 verification does not branch on
    /// secret data. Malformed hex and wrong lengths are refusals, not errors —
    /// both arrive from anyone on the internet.
    /// </remarks>
    private static bool IsSigned(string publicKeyHex, string signatureHex, string timestamp, byte[] body)
    {
        if (string.IsNullOrEmpty(signatureHex) || string.IsNullOrEmpty(timestamp))
        {
            return false;
        }

        byte[] publicKey;
        byte[] signature;
        try
        {
            publicKey = Convert.FromHexString(publicKeyHex);
            signature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            return false;
        }

        if (publicKey.Length != Ed25519.PublicKeySize || signature.Length != Ed25519.SignatureSize)
        {
            return false;
        }

        var timestampBytes = Encoding.UTF8.GetBytes(timestamp);
        var signer = new Ed25519Signer();
        signer.Init(false, new Ed25519PublicKeyParameters(publicKey));
        signer.BlockUpdate(timestampBytes, 0, timestampBytes.Length);
        signer.BlockUpdate(body, 0, body.Length);
        return signer.VerifySignature(signature);
    }

    private bool IsWithinWindow(string timestamp)
    {
        // Unix seconds in bare digits: a sign, a space or a fraction is refused, not guessed at.
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var signedAt))
        {
            return false;
        }

        var now = _dateTimeProvider.Now.ToUnixTimeSeconds();
        var window = (long)TimestampWindow.TotalSeconds;
        return signedAt >= now - window && signedAt <= now + window;
    }
}
