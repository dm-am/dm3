using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Inactivity;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The inactivity pass is a question about a moment in time, and the moment belongs
/// to the caller: the processor reads its clock once and hands the same instant to
/// every query of the pass. Runs against the container Postgres because the
/// selection is one statement comparing a cutoff against two nullable columns.
/// </summary>
public class InactivityRepositoryShould : IntegrationTestBase
{
    public InactivityRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The cutoff used to be measured from the process clock inside the query, so
    /// no test could ask what the rule does a month from now, and the game the
    /// processor stamped was not necessarily selected at the same instant.
    /// </summary>
    [Fact]
    public async Task MeasureTheSilenceFromTheMomentItIsGivenAndNotFromTheProcessClock()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInactivityRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // An hour old by the process clock: a query reading that clock itself would
        // never call this game silent, whatever moment it was asked about.
        var lastPostUtc = DateTimeOffset.UtcNow.AddHours(-1);
        var gameId = await SeedActiveGameAsync(dbContext, lastPostUtc);

        var afterTheThreshold = await repository.GetInactiveGamesToWarn(
            TimeSpan.FromDays(30), lastPostUtc.AddDays(31));
        var beforeTheThreshold = await repository.GetInactiveGamesToWarn(
            TimeSpan.FromDays(30), lastPostUtc.AddDays(29));

        afterTheThreshold.Should().Contain(gameId,
            "thirty-one days of silence have passed at the moment the pass was given");
        beforeTheThreshold.Should().NotContain(gameId,
            "twenty-nine days is inside the threshold, and the same moment decides both ways");
    }

    /// <summary>
    /// A game and a master of its own per run: the fixture's database is shared, and
    /// warning a game somebody else seeded would make the assertion depend on them.
    /// </summary>
    private static async Task<Guid> SeedActiveGameAsync(DmDbContext dbContext, DateTimeOffset lastPostUtc)
    {
        var masterId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = masterId,
            // Username is varchar(20) and uniquely indexed, so the id fills it out.
            Username = $"i{masterId:N}"[..20],
            Email = $"{masterId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var gameId = Guid.NewGuid();
        var suffix = gameId.ToString("N")[..8];
        dbContext.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = $"i{suffix}",
            MasterId = masterId,
            Title = $"Inactivity game {suffix}",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = lastPostUtc.AddDays(-1),
            ActivatedUtc = lastPostUtc.AddDays(-1),
            LastPostCreatedUtc = lastPostUtc,
        });
        await dbContext.SaveChangesAsync();

        return gameId;
    }
}
