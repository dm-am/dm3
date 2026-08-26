using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// A session authenticates only the pair it was issued for (INV-4).
/// </summary>
/// <remarks>
/// The row carries its owner and the lookup filters by both halves of the
/// token: a session id that exists but belongs to somebody else resolves to
/// nothing. The rule used to be held by the document being keyed by its owner;
/// on rows it is the predicate, and the predicate only exists against a live
/// store.
/// </remarks>
public class SessionOwnershipShould : IntegrationTestBase
{
    public SessionOwnershipShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseAValidSessionIdOfAnotherUser()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuthenticationRepository>();
        var owner = await SeedUser(scope);
        var stranger = await SeedUser(scope);
        var session = await repository.AddSession(owner, Session());

        (await repository.FindUserSession(owner, session.Id))
            .Should().NotBeNull("the pair the session was issued for authenticates");
        (await repository.FindUserSession(stranger, session.Id))
            .Should().BeNull("both halves of the token have to agree");
    }

    [Fact]
    public async Task PurgeOnlyTheSessionsThatHaveExpired()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuthenticationRepository>();
        var owner = await SeedUser(scope);
        var expired = await repository.AddSession(owner, Session(expiredHoursAgo: 2));
        var live = await repository.AddSession(owner, Session());

        var purged = await repository.PurgeExpiredSessions(DateTimeOffset.UtcNow);

        purged.SessionsRemoved.Should().BeGreaterThanOrEqualTo(1);
        (await repository.FindUserSession(owner, expired.Id)).Should().BeNull(
            "the purge removes what had expired by the pass");
        (await repository.FindUserSession(owner, live.Id)).Should().NotBeNull(
            "a live session survives every pass");
    }

    private static CreateSession Session(int? expiredHoursAgo = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedUtc = DateTime.UtcNow.AddDays(-1),
        ExpirationUtc = expiredHoursAgo.HasValue
            ? DateTime.UtcNow.AddHours(-expiredHoursAgo.Value)
            : DateTime.UtcNow.AddDays(30),
        Persistent = true,
        IpAddress = "127.0.0.1",
        UserAgent = "test-agent",
        DeviceInfo = "Test on Test"
    };

    private static async Task<Guid> SeedUser(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = $"o{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }
}
