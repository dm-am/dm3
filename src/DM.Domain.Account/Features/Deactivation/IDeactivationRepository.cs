using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Deactivation;

/// <summary>
/// Repository interface for account deactivation
/// </summary>
public interface IDeactivationRepository
{
    /// <summary>
    /// Gets user credentials for password verification during deactivation
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User credentials or null if user not found</returns>
    Task<UserCredentials?> GetUserCredentials(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a user as deactivated (soft delete)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeactivateUser(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// User credentials for deactivation verification
/// </summary>
public class UserCredentials
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Password hash
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password salt
    /// </summary>
    public string Salt { get; set; } = null!;
}
