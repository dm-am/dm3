using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for password reseting
/// </summary>
public interface IPasswordResetApiService
{
    /// <summary>
    /// Reset user password (sends reset token via email).
    /// Always completes silently to prevent user enumeration.
    /// </summary>
    /// <param name="resetPassword">Login and email for verification</param>
    Task Reset(ResetPassword resetPassword);

    /// <summary>
    /// Change user password using old password or reset token
    /// </summary>
    /// <param name="changePassword">Password change request with credentials</param>
    /// <returns>Updated user wrapped in envelope</returns>
    Task<Envelope<User>> Change(ChangePassword changePassword);
}