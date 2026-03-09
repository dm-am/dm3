using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// API service for user activation in email-first registration flow
/// </summary>
public interface IActivationApiService
{
    /// <summary>
    /// Get pending registration info by activation token
    /// </summary>
    /// <param name="token">Activation token from email link</param>
    /// <returns>Pending info if found, null otherwise</returns>
    Task<PendingInfoResponse?> GetPendingInfo(Guid token);

    /// <summary>
    /// Complete activation with chosen username and authenticate
    /// </summary>
    /// <param name="token">Activation token from URL path</param>
    /// <param name="request">Activation request with username</param>
    /// <param name="httpContext">HTTP context for setting auth cookie</param>
    /// <returns>Activated user wrapped in envelope</returns>
    Task<Envelope<User>> Activate(Guid token, ActivationRequest request, HttpContext httpContext);

    /// <summary>
    /// Resend activation email for pending registration
    /// </summary>
    /// <param name="resendActivation">Email address</param>
    Task ResendActivation(ResendActivation resendActivation);
}
