using DM.Infrastructure.Persistence;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// A user row of a test's own, written straight to the database.
/// </summary>
/// <remarks>
/// The fixture database is shared and seeded, so a fixed identifier would depend
/// on whoever else wrote one. Username is varchar(20) and uniquely indexed, so the
/// id is truncated behind the prefix rather than used whole.
/// </remarks>
internal static class UserRows
{
    /// <summary>Writes the row and answers its id.</summary>
    /// <param name="dbContext">The context the row is saved through.</param>
    /// <param name="prefix">Letters that tell the suite's users from the others.</param>
    /// <param name="lastActivityUtc">When the user was last seen; never when null.</param>
    public static async Task<Guid> AddAsync(
        DmDbContext dbContext, string prefix, DateTimeOffset? lastActivityUtc = null)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = lastActivityUtc,
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }
}
