using System;
using System.Threading.Tasks;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// API service for credential management (password, email, username)
/// </summary>
public interface ICredentialsApiService
{
    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    /// <param name="request">Current and new passwords</param>
    /// <returns>Updated user</returns>
    Task<User> ChangePassword(PasswordChangeRequest request);

    /// <summary>
    /// Request email change (sends confirmation to new email)
    /// </summary>
    /// <param name="request">Password and new email</param>
    /// <returns>Updated user</returns>
    Task<User> RequestEmailChange(EmailChangeRequest request);

    /// <summary>
    /// Confirm email change by token
    /// </summary>
    /// <param name="token">Confirmation token from email</param>
    Task ConfirmEmailChange(Guid token);

    /// <summary>
    /// Create username change request (requires moderator approval)
    /// </summary>
    /// <param name="request">Reason for change</param>
    /// <returns>Created request</returns>
    Task<UsernameChangeResponse> RequestUsernameChangeAsync(UsernameChangeCreateRequest request);

    /// <summary>
    /// Get current user's username change request status
    /// </summary>
    /// <returns>Request status or null if none exists</returns>
    Task<UsernameChangeResponse?> GetUsernameChangeStatusAsync();

    /// <summary>
    /// Get the state of an approval token
    /// </summary>
    /// <param name="token">Approval token from notification</param>
    /// <returns>Token state, or null when no request was issued for it</returns>
    Task<UsernameChangeApprovalInfo?> GetUsernameChangeApprovalAsync(Guid token);

    /// <summary>
    /// Complete username change with chosen name
    /// </summary>
    /// <param name="token">Approval token</param>
    /// <param name="request">Chosen username</param>
    /// <returns>Updated request</returns>
    Task<UsernameChangeResponse> CompleteUsernameChangeAsync(Guid token, UsernameChangeCompletionRequest request);
}
