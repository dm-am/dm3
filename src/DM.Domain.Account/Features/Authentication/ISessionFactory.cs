using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Factory for a user session
/// </summary>
internal interface ISessionFactory
{
    /// <summary>
    /// Creates a session DTO to be stored in DB
    /// </summary>
    /// <param name="persistent">Persistence flag</param>
    /// <param name="context">Session context with device info</param>
    /// <returns>Session DTO</returns>
    CreateSession Create(bool persistent, SessionContext? context = null);
}
