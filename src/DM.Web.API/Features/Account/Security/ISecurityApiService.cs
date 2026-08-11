using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Account.Security;

/// <summary>
/// API service for security event log
/// </summary>
public interface ISecurityApiService
{
    /// <summary>
    /// Get security events log for current user
    /// </summary>
    /// <param name="type">Optional filter by event type (login, password, session)</param>
    /// <param name="limit">Maximum number of events to return</param>
    /// <returns>List of security events</returns>
    Task<IEnumerable<SecurityEvent>> GetSecurityLogs(SecurityLogType? type = null, int limit = 50);
}
