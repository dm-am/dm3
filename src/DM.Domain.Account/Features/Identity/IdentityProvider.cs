using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;
using Serilog.Context;

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
        set
        {
            _identity = value;
            if (_identity?.User != null)
            {
                LogContext.PushProperty("User", _identity.User.Username);
            }
        }
    }

    /// <inheritdoc />
    public IAuthorizationSubject CurrentSubject => _identity?.User ?? AuthenticatedUser.Guest;

    /// <inheritdoc />
    public void Refresh()
    {
        if (_identity != null)
        {
            Current = Current;
        }
    }
}