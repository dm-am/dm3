using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange;

/// <summary>
/// Storage for email changing
/// </summary>
internal interface IEmailChangeRepository
{
    /// <summary>
    /// Find user by login
    /// </summary>
    /// <param name="login"></param>
    /// <returns></returns>
    Task<AuthenticatedUser?> FindUser(string login);

    /// <summary>
    /// Check if email is not taken by another user
    /// </summary>
    Task<bool> IsEmailFree(string email, CancellationToken ct);

    /// <summary>
    /// Update user email
    /// </summary>
    Task Update(IUpdateBuilder<User> updateUser, Token token);

    /// <summary>
    /// Invalidate old email change tokens for a user
    /// </summary>
    Task InvalidateOldEmailChangeTokens(Guid userId);
}