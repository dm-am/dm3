namespace DM.Domain.Core.Identity;

/// <summary>
/// Stores current user identity
/// </summary>
public interface IIdentitySetter
{
    /// <summary>
    /// Current user identity
    /// </summary>
    IIdentity Current { set; }
}
