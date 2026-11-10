using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Users;

/// <summary>
/// Minimal user lookup service for cross-module operations.
/// Used by Blog, Game, Moderation modules to resolve users without importing Domain.Personal.
/// </summary>
public interface IUserLookupService
{
    /// <summary>
    /// Get user by username (throws HttpException if not found)
    /// </summary>
    /// <param name="username">Username to find</param>
    /// <returns>User info</returns>
    /// <exception cref="DM.Domain.Core.Exceptions.HttpException">User not found (404 Not Found)</exception>
    Task<GeneralUser> GetAsync(string username);

    /// <summary>
    /// Get user by ID (throws HttpException if not found)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>User info</returns>
    /// <exception cref="DM.Domain.Core.Exceptions.HttpException">User not found (404 Not Found)</exception>
    Task<GeneralUser> GetAsync(Guid userId);

    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Find user ID by username (for game/blog resolvers)
    /// </summary>
    /// <param name="username">Username to find</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple with Found flag and UserId (Empty if not found)</returns>
    Task<(bool Found, Guid UserId)> FindUserIdAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Get the display-level essentials of several users at once
    /// </summary>
    /// <remarks>
    /// One projection and no counter hydration, see <see cref="UserReference"/>.
    /// Here rather than on the read repository: the API layer assembles answers from
    /// domain calls, not from repositories.
    /// </remarks>
    /// <param name="userIds">User identifiers</param>
    /// <returns>References of the users that exist</returns>
    Task<IEnumerable<UserReference>> GetReferencesAsync(IEnumerable<Guid> userIds);

    /// <summary>
    /// Identifiers of every removed account, for a list asked by host or author for
    /// "Аккаунт удален" (<see cref="RemovedAccount" />). They narrow the list on the
    /// server and are never part of an answer.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetRemovedAccountIdsAsync();
}
