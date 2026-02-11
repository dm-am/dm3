using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Services.Users;

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
    /// Complete activation with chosen Login and authenticate
    /// </summary>
    /// <param name="request">Activation request with token and login</param>
    /// <param name="httpContext">HTTP context for setting auth cookie</param>
    /// <returns>Activated user wrapped in envelope</returns>
    Task<Envelope<User>> Activate(ActivationRequest request, HttpContext httpContext);

    /// <summary>
    /// Check if a login is available for registration
    /// </summary>
    /// <param name="login">Login to check</param>
    /// <returns>Availability result</returns>
    Task<LoginAvailabilityResponse> CheckLoginAvailability(string login);

    /// <summary>
    /// Resend activation email for pending registration
    /// </summary>
    /// <param name="resendActivation">Email address</param>
    Task ResendActivation(ResendActivation resendActivation);
}
