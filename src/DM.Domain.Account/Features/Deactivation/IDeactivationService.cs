using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Deactivation;

/// <summary>
/// Service for account deactivation (soft delete)
/// </summary>
public interface IDeactivationService
{
    /// <summary>
    /// Deactivate the current user's account
    /// </summary>
    /// <param name="password">Current password for confirmation</param>
    /// <returns>Task that completes when deactivation is done</returns>
    Task Deactivate(string password);
}
