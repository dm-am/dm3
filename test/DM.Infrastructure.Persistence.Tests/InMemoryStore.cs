using System;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests;

internal static class InMemoryStore
{
    public static DmDbContext Open() => Open(Guid.NewGuid().ToString());

    public static DmDbContext Open(string database) => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(database)
        .Options);

    // A user row with no way to sign in, there so that a post or a message whose author is a
    // required navigation has someone to join.
    public static DbUser User(Guid userId, string username) => new()
    {
        UserId = userId,
        Username = username,
        Email = $"{username}@test.local",
        Salt = "",
        PasswordHash = "",
    };
}
