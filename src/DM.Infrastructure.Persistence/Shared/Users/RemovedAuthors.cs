using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// What a removed account wrote is read with the account row in reach, and signed
/// by nobody.
/// </summary>
/// <remarks>
/// EF joins a required author navigation to a filtered user row with an INNER JOIN,
/// so the content of a removed account would vanish with it. The filter over Users is
/// a named one, and content reads lift only it through <see cref="KeepingRemovedAuthors{T}" />;
/// <see cref="GeneralUserProjections.Projection" /> then answers null for a removed account.
/// Everything else in a lifted query that reads a user says what it does with a removed one.
/// </remarks>
internal static class RemovedAuthors
{
    /// <summary>
    /// The key of the soft-delete filter over Users. Every other removable entity
    /// keeps the anonymous filter, and lifting this key leaves those in force.
    /// </summary>
    public const string AccountFilter = "RemovedAccounts";

    /// <summary>
    /// The query keeps the rows removed accounts wrote; nested users are then
    /// answered by the formulas that return null for a removed account.
    /// </summary>
    public static IQueryable<T> KeepingRemovedAuthors<T>(this IQueryable<T> query)
        where T : class =>
        query.IgnoreQueryFilters([AccountFilter]);
}
