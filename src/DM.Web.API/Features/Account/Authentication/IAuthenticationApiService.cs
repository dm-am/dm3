using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// API service for user authentication and session management
/// </summary>
public interface IAuthenticationApiService
{
    /// <summary>
    /// Login using email-password credentials
    /// </summary>
    /// <param name="request">Email and password</param>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Login response with user and preferences</returns>
    Task<LoginResponse> Login(LoginRequest request, HttpContext httpContext);

    /// <summary>
    /// Logout current session
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    Task Logout(HttpContext httpContext);

    /// <summary>
    /// Logout from all devices except current, which stays as it is
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    Task LogoutElsewhere(HttpContext httpContext);

    /// <summary>
    /// Get all active sessions for current user
    /// </summary>
    /// <returns>List of sessions</returns>
    Task<IEnumerable<Session>> GetSessions();

    /// <summary>
    /// Terminate a specific session
    /// </summary>
    /// <param name="sessionId">Session ID to terminate</param>
    Task TerminateSession(Guid sessionId);
}
