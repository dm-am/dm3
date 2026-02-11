using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <summary>
/// Creates new user DAL models
/// </summary>
internal interface IUserFactory
{
    /// <summary>
    /// Create new user from pending registration and chosen login
    /// </summary>
    /// <param name="pending">Pending registration with email and password</param>
    /// <param name="login">Chosen login (username)</param>
    /// <returns>DAL model for user</returns>
    User CreateFromPending(PendingRegistration pending, string login);
}
