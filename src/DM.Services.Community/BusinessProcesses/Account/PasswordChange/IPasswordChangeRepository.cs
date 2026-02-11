using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordChange;

/// <summary>
/// Storage for password change
/// </summary>
internal interface IPasswordChangeRepository
{
    /// <summary>
    /// Find existing user
    /// </summary>
    /// <param name="login"></param>
    /// <returns></returns>
    Task<AuthenticatedUser?> FindUser(string login);

    /// <summary>
    /// Find token user
    /// </summary>
    /// <param name="tokenId"></param>
    /// <returns></returns>
    Task<AuthenticatedUser?> FindUser(Guid tokenId);

    /// <summary>
    /// Check if token is valid
    /// </summary>
    /// <param name="tokenId"></param>
    /// <param name="createdSince"></param>
    /// <returns></returns>
    Task<bool> TokenValid(Guid tokenId, DateTimeOffset createdSince);

    /// <summary>
    /// Save user password changes
    /// </summary>
    /// <param name="userUpdate"></param>
    /// <param name="tokenUpdate"></param>
    /// <returns></returns>
    Task UpdatePassword(IUpdateBuilder<User> userUpdate, IUpdateBuilder<Token>? tokenUpdate);

    /// <summary>
    /// Check if password has been used before (last N entries)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="newPassword">New password to check</param>
    /// <param name="checkCount">Number of recent passwords to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if password has been used before</returns>
    Task<bool> IsPasswordReused(Guid userId, string newPassword, int checkCount, CancellationToken ct);

    /// <summary>
    /// Save current password hash to history
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="passwordHash">Password hash</param>
    /// <param name="salt">Password salt</param>
    /// <param name="version">Hash version</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task SavePasswordToHistory(Guid userId, string passwordHash, string salt, int version, CancellationToken ct);

    /// <summary>
    /// Cleanup old password history entries
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="keepCount">Number of entries to keep</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task CleanupOldEntries(Guid userId, int keepCount, CancellationToken ct);
}