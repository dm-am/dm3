using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Inactivity;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

// Against the container Postgres: one statement compares a cutoff against two nullable columns.
public class InactivityRepositoryShould : IntegrationTestBase
{
    public InactivityRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

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

    // The selected state is carried into the WHERE of the write, which checks it at the moment of
    // writing.
    [Fact]
    public async Task RefuseToFreezeAGameThatWasPostedInBeforeTheWriteReachedIt()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInactivityRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var now = DateTimeOffset.UtcNow;
        var warnedUtc = now.AddDays(-8);
        var gameId = await SeedActiveGameAsync(dbContext, warnedUtc.AddDays(-30));
        await StampInactivityWarningAsync(dbContext, gameId, warnedUtc);

        var selected = await repository.GetWarnedGamesToFreeze(TimeSpan.FromDays(7), now);
        selected.Should().Contain(gameId, "a week of silence has passed since the warning");

        // The master posts. The posting path stamps the moment and clears the
        // warning as it lands, which is the state the freeze now runs into.
        await dbContext.Set<DbGame>()
            .Where(g => g.GameId == gameId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.LastPostCreatedUtc, now)
                .SetProperty(g => g.InactivityWarningUtc, (DateTimeOffset?)null));

        var frozen = await repository.FreezeGame(gameId, now);

        frozen.Should().BeFalse("the game the pass selected is not the game the write found");
        var game = await dbContext.Set<DbGame>().AsNoTracking().FirstAsync(g => g.GameId == gameId);
        game.Status.Should().Be(ModuleStatus.Active, "a game with a fresh post in it keeps running");
        game.ClosedReason.Should().Be(ClosedReason.None);
    }

    // A post clears the warning stamp as it lands, so the stamp checks the state it was selected
    // in.
    [Fact]
    public async Task RefuseToWarnAGameThatWasPostedInBeforeTheWriteReachedIt()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInactivityRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // A moment with no sub-microsecond part: PostgreSQL stores microseconds, and
        // the stamp is compared with what was handed in.
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var threshold = TimeSpan.FromDays(30);
        var gameId = await SeedActiveGameAsync(dbContext, now.AddDays(-31));

        var selected = await repository.GetInactiveGamesToWarn(threshold, now);
        selected.Should().Contain(gameId, "a month of silence has passed");

        // The master posts, between the selection and the write the pass makes off
        // it. The posting path stamps the moment and clears the warning as it lands.
        await dbContext.Set<DbGame>()
            .Where(g => g.GameId == gameId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.LastPostCreatedUtc, now)
                .SetProperty(g => g.InactivityWarningUtc, (DateTimeOffset?)null));

        var warned = await repository.SetInactivityWarning(gameId, threshold, now);

        warned.Should().BeFalse("the game the pass selected is not the game the write found");
        var game = await dbContext.Set<DbGame>().AsNoTracking().FirstAsync(g => g.GameId == gameId);
        game.InactivityWarningUtc.Should().BeNull(
            "a warning stamped over a fresh post starts the week that ends in a freeze");
    }

    // The game that is still silent when the write reaches it is warned, so the predicate above
    // refuses the right thing rather than everything.
    [Fact]
    public async Task WarnTheGameThatIsStillSilentWhenTheWriteReachesIt()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInactivityRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // A moment with no sub-microsecond part: PostgreSQL stores microseconds, and
        // the stamp is compared with what was handed in.
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var threshold = TimeSpan.FromDays(30);
        var gameId = await SeedActiveGameAsync(dbContext, now.AddDays(-31));

        var warned = await repository.SetInactivityWarning(gameId, threshold, now);

        warned.Should().BeTrue();
        var game = await dbContext.Set<DbGame>().AsNoTracking().FirstAsync(g => g.GameId == gameId);
        game.InactivityWarningUtc.Should().Be(now, "the pass stamps the moment it ran at");
    }

    private static Task StampInactivityWarningAsync(
        DmDbContext dbContext, Guid gameId, DateTimeOffset warnedUtc) =>
        dbContext.Set<DbGame>()
            .Where(g => g.GameId == gameId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.InactivityWarningUtc, warnedUtc));

    // A game and a master of its own per run: the fixture's database is shared, and warning a game
    // somebody else seeded would make the assertion depend on them.
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
