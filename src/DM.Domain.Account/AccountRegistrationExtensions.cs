using DM.Domain.Account.Features.Identity;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DM.Domain.Account;

/// <summary>
/// Lifetimes this assembly's types need beyond the assembly scan's default.
/// </summary>
/// <remarks>
/// The scan registers everything per dependency; the two types below must be one
/// instance per scope, and that requirement belongs next to them rather than in a
/// host: TokenFactory is internal, so a host could only name it by namespace
/// string, and a rename then fails at start-up instead of at compile time. Every
/// host that needs the account domain calls this instead of restating the
/// contract.
/// </remarks>
public static class AccountRegistrationExtensions
{
    /// <summary>
    /// Registers the account types whose lifetimes the scan default cannot serve.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDmAccount(this IServiceCollection services)
    {
        // The setter and the provider have to be the same object within a request:
        // authentication writes the identity through IIdentitySetter and every
        // consumer reads it through IIdentityProvider. Forwarding factories, not
        // three registrations of the type: naive ones construct three objects per
        // scope and the identity every consumer reads stays null.
        services.TryAddScoped<IdentityProvider>();
        services.TryAddScoped<IIdentitySetter>(
            provider => provider.GetRequiredService<IdentityProvider>());
        services.TryAddScoped<IIdentityProvider>(
            provider => provider.GetRequiredService<IdentityProvider>());
        services.TryAddScoped<IAuthorizationContextProvider>(
            provider => provider.GetRequiredService<IdentityProvider>());

        services.TryAddScoped<ITokenFactory, TokenFactory>();

        return services;
    }
}
