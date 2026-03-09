namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Creates new user DTOs
/// </summary>
internal interface IUserFactory
{
    /// <summary>
    /// Create new user from pending registration and chosen username
    /// </summary>
    /// <param name="pending">Pending registration with email and password</param>
    /// <param name="username">Chosen username</param>
    /// <returns>CreateUser DTO</returns>
    CreateUser CreateFromPending(PendingRegistration pending, string username);
}
