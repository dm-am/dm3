using System;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.PasswordChange;

/// <summary>
/// Storage for password change
/// </summary>
public interface IPasswordChangeRepository
{
    /// <summary>
    /// Find existing user by username
    /// </summary>
    Task<AuthenticatedUser?> FindUser(string username);

    /// <summary>
    /// Find user by token
    /// </summary>
    Task<AuthenticatedUser?> FindUser(Guid tokenId);

    /// <summary>
    /// Check if token is valid
    /// </summary>
    Task<bool> TokenValid(Guid tokenId, DateTimeOffset createdSince);

    /// <summary>
    /// Save user password changes
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="passwordHash">New password hash</param>
    /// <param name="salt">New password salt</param>
    /// <param name="tokenIdToInvalidate">Optional token to mark as removed</param>
    Task UpdatePassword(Guid userId, string passwordHash, string salt, Guid? tokenIdToInvalidate);
}
