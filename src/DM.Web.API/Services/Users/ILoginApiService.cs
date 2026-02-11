using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.Core.Authentication.Credentials;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for user authentication
/// </summary>
public interface ILoginApiService
{
    /// <summary>
    /// Login using login-password tuple
    /// </summary>
    /// <param name="credentials">Login and password</param>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Authenticated user</returns>
    Task<Envelope<User>> Login(LoginCredentials credentials, HttpContext httpContext);

    /// <summary>
    /// Logout as current user
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    Task Logout(HttpContext httpContext);

    /// <summary>
    /// Logout as current user from every device with active session
    /// And create one new session
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    Task LogoutAll(HttpContext httpContext);

    /// <summary>
    /// Get current user
    /// </summary>
    /// <returns>Current user</returns>
    Task<Envelope<UserDetails>> GetCurrent();

    /// <summary>
    /// Get all active sessions for current user
    /// </summary>
    /// <returns>List of sessions</returns>
    Task<ListEnvelope<SessionInfo>> GetSessions();

    /// <summary>
    /// Update current user settings
    /// </summary>
    /// <param name="settings">New settings</param>
    /// <returns>Updated settings</returns>
    Task<Envelope<UserSettings>> UpdateSettings(UserSettings settings);

    /// <summary>
    /// Terminate a specific session
    /// </summary>
    /// <param name="sessionId">Session ID to terminate</param>
    Task TerminateSession(System.Guid sessionId);
}