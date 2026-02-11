using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Users.LoginHistory;

/// <summary>
/// Service for reading login history
/// </summary>
public interface ILoginHistoryService
{
    /// <summary>
    /// Get login history for a user by login
    /// </summary>
    Task<IReadOnlyCollection<LoginHistoryEntry>> GetByLogin(string login);
}
