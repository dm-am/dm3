using DM.Infrastructure.Persistence;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <remarks>
/// The fixture database is shared and seeded, so a fixed identifier would depend
/// on whoever else wrote one. Username is varchar(20) and uniquely indexed, so the
/// id is truncated behind the prefix rather than used whole.
/// </remarks>
internal static class UserRows
{
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

    /// <summary>Adds the row to the context unsaved and answers it, for a seed that saves once at its end.</summary>
    public static DbUser Add(DmDbContext dbContext, string prefix, string emailDomain)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@{emailDomain}",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        return user;
    }
}
