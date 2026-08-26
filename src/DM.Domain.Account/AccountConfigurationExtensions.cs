using System;
using DM.Domain.Account.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Domain.Account;

/// <summary>
/// Configuration the account domain reads.
/// </summary>
/// <remarks>
/// Ships next to <see cref="AccountRegistrationExtensions"/> for the same reason
/// that call exists: a host that registers the account types must not be able to leave
/// their options unbound. IOptions of an unbound type does not throw — it hands
/// out a default instance — so the omission surfaces as a wrong answer on the
/// first request that reaches the type, not as a failure to start.
/// </remarks>
public static class AccountConfigurationExtensions
{
    /// <summary>
    /// Binds the account options and refuses to start without a usable
    /// encryption key.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration to read the sections from.</param>
    public static IServiceCollection AddDmAccountConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        // The session and token encryption key has no in-repo default on purpose:
        // a deployment that silently inherits a key from the repository has no
        // secret at all, and every session token becomes forgeable by anyone who
        // can read the source. Missing key must stop the host, not surface on the
        // first authenticated request.
        services.AddOptions<CryptoConfiguration>()
            .Bind(configuration.GetSection(nameof(CryptoConfiguration)))
            .Validate(IsUsableEncryptionKey,
                "CryptoConfiguration:KeyBase64 must be a base64-encoded 32-byte key. " +
                "Generate one with `openssl rand -base64 32` and supply it as " +
                "DM_CryptoConfiguration__KeyBase64. It lives on the application " +
                "instance and nowhere else: an edge that serves another address of " +
                "the site runs no application code and has nothing to decrypt.")
            .ValidateOnStart();

        services.Configure<AuthenticationConfiguration>(
            configuration.GetSection(nameof(AuthenticationConfiguration)).Bind);
        services.Configure<TokenConfiguration>(
            configuration.GetSection(nameof(TokenConfiguration)).Bind);
        services.Configure<PasswordPolicyConfiguration>(
            configuration.GetSection(nameof(PasswordPolicyConfiguration)).Bind);
        services.Configure<TwoFactorConfiguration>(
            configuration.GetSection(nameof(TwoFactorConfiguration)).Bind);

        return services;
    }

    private static bool IsUsableEncryptionKey(CryptoConfiguration crypto)
    {
        if (string.IsNullOrWhiteSpace(crypto.KeyBase64))
        {
            return false;
        }

        // AES-256 takes exactly 32 bytes. A shorter value would be rejected later
        // by the crypto service, on the first request instead of at startup.
        Span<byte> key = stackalloc byte[64];
        return Convert.TryFromBase64String(crypto.KeyBase64, key, out var written) && written == 32;
    }
}
