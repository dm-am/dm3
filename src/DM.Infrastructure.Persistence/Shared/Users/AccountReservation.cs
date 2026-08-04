using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// SSOT for the question "is this address or this login already held by an account".
/// </summary>
/// <remarks>
/// An account holds its address and its login for as long as its row exists, deactivation
/// included: deactivation only sets IsRemoved, while the unique indexes over lower("Email")
/// and lower("Username") asserted by
/// <see cref="DM.Infrastructure.Persistence.RelationalStorage.ExpressionIndexInitializer" />
/// cover the whole table with no predicate. That permanence is the product rule and not an
/// oversight: the deactivated row keeps authoring everything the person ever posted, so
/// handing its login to somebody else would re-point every mention and every profile link,
/// and the same permanence is already granted to abandoned logins by the unique
/// UsernameHistories.OldUsername. The rule also has to survive the DM2 import, which resolves
/// its case-insensitive collisions once, at import time, instead of parking them behind a
/// deletion flag that any restored row would blow up.
///
/// Hence both queries ignore the global soft-delete filter and compare by lower(), which is
/// what the indexes compare. Asked any other way a check reports free what the database will
/// refuse, and the refusal arrives as a 500 from whatever writes the row. For registration
/// that is the click on the confirmation link, after the letter has already been sent.
/// </remarks>
internal static class AccountReservation
{
    /// <summary>
    /// The address is held by an account, deactivated accounts included
    /// </summary>
    public static Task<bool> EmailTaken(
        DbSet<User> users,
        string email,
        CancellationToken ct = default) =>
        users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct);

    /// <summary>
    /// The login is held by an account, deactivated accounts included.
    /// <paramref name="exceptUserId" /> leaves one account out, for the caller asking whether
    /// that account may take the login back.
    /// </summary>
    public static Task<bool> UsernameTaken(
        DbSet<User> users,
        string username,
        Guid? exceptUserId = null,
        CancellationToken ct = default)
    {
        IQueryable<User> query = users.IgnoreQueryFilters();
        if (exceptUserId.HasValue)
        {
            query = query.Where(u => u.UserId != exceptUserId.Value);
        }

        return query.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct);
    }
}
