using System;
using System.Buffers;
using DM.Infrastructure.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace DM.Web.API.Features.Personal.Webhooks;

internal static class DiscordPublicKeyExtensions
{
    /// <summary>
    /// Refuses to start with a Discord public key the signature check cannot use.
    /// </summary>
    /// <remarks>
    /// The verifier throws on a key that is no point of the curve, so every interaction would answer
    /// 500. Declared by this host and not where the section is bound: no other host verifies with the
    /// key. An empty key passes, it closes the webhook.
    /// </remarks>
    public static IServiceCollection ValidateDiscordPublicKey(this IServiceCollection services)
    {
        services.AddOptions<BotConfiguration>()
            .Validate(
                bot => string.IsNullOrEmpty(bot.DiscordPublicKey) || IsEd25519PublicKey(bot.DiscordPublicKey),
                "BotConfiguration:DiscordPublicKey must be the Public Key of the Discord application, " +
                "64 hex characters of an Ed25519 key. Leave it empty to keep the webhook closed.")
            .ValidateOnStart();
        return services;
    }

    private static bool IsEd25519PublicKey(string hex)
    {
        Span<byte> key = stackalloc byte[Ed25519.PublicKeySize];
        return hex.Length == 2 * Ed25519.PublicKeySize
               && Convert.FromHexString(hex, key, out _, out _) == OperationStatus.Done
               && Ed25519.ValidatePublicKeyFull(key);
    }
}
