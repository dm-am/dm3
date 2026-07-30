using Autofac;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Account.Features.Tokens;

namespace DM.Domain.Account;

/// <summary>
/// Lifetimes this module's types need beyond the assembly scan's default.
/// </summary>
/// <remarks>
/// The scan registers everything per dependency; the two types below must be one
/// instance per scope, and that requirement belongs next to them. It used to live
/// in the API host, which named them by namespace string because they are
/// internal — a rename would have returned null and taken the host down on start,
/// with nothing at compile time to catch it. Every host that needs the account
/// domain registers this module instead of restating the contract.
/// </remarks>
public class AccountModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // The setter and the provider have to be the same object within a request:
        // authentication writes the identity through IIdentitySetter and every
        // consumer reads it through IIdentityProvider.
        builder.RegisterType<IdentityProvider>()
            .AsSelf()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        builder.RegisterType<TokenFactory>()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();
    }
}
