namespace DM.Domain.Core.Authorization;

/// <summary>
/// Provides current authorization context (user) for permission checks.
/// Implemented by identity system to bridge between authentication and authorization.
/// </summary>
public interface IAuthorizationContextProvider
{
    /// <summary>
    /// Current user for authorization checks
    /// </summary>
    IAuthorizationSubject CurrentSubject { get; }
}
