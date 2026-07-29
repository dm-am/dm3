using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Identity;

/// <summary>
/// Current user identity storage. Public because hosts without an HTTP pipeline
/// of their own (the workers) must register it explicitly: the assembly scan
/// registers per dependency, and the setter and the provider have to be one
/// instance within a scope.
/// </summary>
public class IdentityProvider : IIdentitySetter, IIdentityProvider, IAuthorizationContextProvider
{
    private IIdentity _identity = null!;

    /// <inheritdoc cref="IdentityProvider" />
    public IIdentity Current
    {
        get => _identity;
        set => _identity = value;
    }

    /// <inheritdoc />
    public IAuthorizationSubject CurrentSubject => _identity?.User ?? AuthenticatedUser.Guest;
}