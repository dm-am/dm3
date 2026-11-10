using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Users;

/// <summary>
/// Read-only repository for user data. Used by Community and Moderation modules
/// for accessing user profile information without importing Domain.Personal.
/// </summary>
public interface IUserReadRepository
{
    // Single user

    Task<GeneralUser?> GetUserAsync(string username);

    /// <summary>
    /// Identifier of a live user by username, or null when there is none.
    /// </summary>
    /// <remarks>
    /// A single scalar SELECT, unlike <see cref="GetUserAsync(string)"/>, which
    /// hydrates the achievement counters.
    /// </remarks>
    Task<Guid?> FindUserIdAsync(string username);

    Task<GeneralUser?> GetUserAsync(Guid userId);

    Task<UserDetails?> GetUserDetailsAsync(string username);

    Task<UserDetails?> GetUserDetailsAsync(Guid userId);

    // List users

    /// <summary>
    /// Count users matching the filter. Takes the same filter object as
    /// <see cref="GetUsersAsync(PagingData, UserFilter)" /> so the total cannot
    /// describe a different set than the page does.
    /// </summary>
    Task<int> CountUsersAsync(UserFilter filter);

    /// <summary>
    /// Get users list with pagination
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetUsersAsync(PagingData paging, UserFilter filter);

    Task<IEnumerable<GeneralUser>> GetUsersByRoleAsync(UserRole role);

    /// <remarks>
    /// Hydrates the achievement counters, one query each.
    /// A caller that only renders names and badges wants
    /// <see cref="GetUserReferencesAsync" /> instead.
    /// </remarks>
    Task<IEnumerable<GeneralUser>> GetUsersAsync(IEnumerable<Guid> userIds);

    /// <summary>
    /// Get the display-level essentials of users by IDs: one projection, no
    /// counter hydration.
    /// </summary>
    /// <remarks>
    /// A separate method rather than a flag on <see cref="GetUsersAsync(IEnumerable{Guid})" />
    /// so the return type cannot promise fields nobody filled: a
    /// <see cref="GeneralUser" /> with zeroed counters is
    /// indistinguishable from a user who really has none.
    /// </remarks>
    Task<IEnumerable<UserReference>> GetUserReferencesAsync(IEnumerable<Guid> userIds);

    /// <summary>
    /// Identifiers of every removed account: the rows behind "Аккаунт удален"
    /// (<see cref="RemovedAccount" />).
    /// </summary>
    /// <remarks>
    /// For narrowing a list by host or author on the server. The identifiers never
    /// travel further than the query they narrow.
    /// </remarks>
    Task<IReadOnlyCollection<Guid>> GetRemovedAccountIdsAsync();

    /// <summary>
    /// The profile of "Аккаунт удален": the public counters of every removed account,
    /// added up, and nothing else of them.
    /// </summary>
    /// <remarks>
    /// Sums of what each profile already shows in public - the rating, the posts
    /// written, the posts rated - and the games and blogs where a removed account
    /// hosts or plays, each counted once however many removed accounts it holds. No
    /// name, address, date or identifier of any removed row is part of it.
    /// </remarks>
    Task<GeneralUser> GetRemovedAccountSummaryAsync();
}
