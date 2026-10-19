using System;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// The setup repository tests over the in-memory provider share.
/// </summary>
internal static class InMemoryStore
{
    /// <summary>A context on an in-memory store of its own, which no other test sees.</summary>
    public static DmDbContext Open() => Open(Guid.NewGuid().ToString());

    /// <summary>A context on the named in-memory store: every context on one name sees the same rows.</summary>
    public static DmDbContext Open(string database) => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(database)
        .Options);

    /// <summary>
    /// A user row with no way to sign in, there so that a post or a message
    /// whose author is a required navigation has someone to join.
    /// </summary>
    public static DbUser User(Guid userId, string username) => new()
    {
        UserId = userId,
        Username = username,
        Email = $"{username}@test.local",
        Salt = "",
        PasswordHash = "",
    };
}
