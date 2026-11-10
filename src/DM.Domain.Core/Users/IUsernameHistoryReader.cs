namespace DM.Domain.Core.Users;

/// <summary>
/// Read-only interface for username history access.
/// Used by Community module to display username history in profiles.
/// </summary>
public interface IUsernameHistoryReader
{
    Task<IReadOnlyCollection<UsernameHistoryEntry>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
