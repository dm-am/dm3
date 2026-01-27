using DM.Services.Authentication.Dto;
using Serilog.Context;

namespace DM.Services.Authentication.Implementation.UserIdentity;

/// <summary>
/// Current user identity storage
/// </summary>
internal class IdentityProvider : IIdentitySetter, IIdentityProvider
{
    private IIdentity _identity;

    /// <inheritdoc cref="IdentityProvider" />
    public IIdentity Current
    {
        get => _identity;
        set
        {
            _identity = value;
            if (_identity?.User != null)
            {
                LogContext.PushProperty("User", _identity.User.Login);
            }
        }
    }

    /// <inheritdoc />
    public void Refresh()
    {
        if (_identity != null)
        {
            Current = Current;
        }
    }
}