using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;
using Serilog.Context;

namespace DM.Domain.Account.Features.Identity;

/// <summary>
/// Current user identity storage
/// </summary>
internal class IdentityProvider : IIdentitySetter, IIdentityProvider, IAuthorizationContextProvider
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