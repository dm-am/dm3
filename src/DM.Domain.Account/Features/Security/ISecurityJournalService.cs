using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// The security journal as its owner reads it
/// </summary>
/// <remarks>
/// The audit repository answers four separate questions and takes the subject as
/// an argument; this answers the one question a caller actually has — "my journal,
/// filtered like so" — and takes the subject from the identity. The choice of
/// which query answers which filter, and the refusal to answer for anybody but
/// the current user, are decisions rather than mapping, and a decision made above
/// the domain is made twice the day a second caller appears.
/// </remarks>
public interface ISecurityJournalService
{
    /// <summary>
    /// Read the current user's security events, newest first
    /// </summary>
    /// <param name="type">Filter, or null for the whole journal</param>
    /// <param name="take">Maximum number of events to return</param>
    /// <exception cref="DM.Domain.Core.Exceptions.HttpException">Caller is not authenticated (401)</exception>
    Task<IReadOnlyList<SecurityAuditEntry>> GetOwnAsync(SecurityLogType? type = null, int take = 50);
}
