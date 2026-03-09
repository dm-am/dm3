namespace DM.Domain.Core.Identity;

/// <summary>
/// Provides the current user identity
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// Current user identity
    /// </summary>
    IIdentity Current { get; }
}
