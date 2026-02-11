using System;

namespace DM.Web.Core.Authentication.Credentials;

/// <summary>
/// Unconditional login credentials (for auto-login after activation)
/// </summary>
public class UnconditionalCredentials : AuthCredentials
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }
}