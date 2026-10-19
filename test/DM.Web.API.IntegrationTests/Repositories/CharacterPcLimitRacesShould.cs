using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// A game never announces fewer seats than it has players sitting in them.
/// </summary>
/// <remarks>
/// Nothing recomputes the stated limit, and a count cannot see an uncommitted character. Each path is checked by a
/// concurrent pair and by a pair holding one activation open by hand; only a live database shows a write waiting.
/// </remarks>
public class CharacterPcLimitRacesShould : IntegrationTestBase
{
    /// <summary>
    /// Enough callers that the interleaving the defect needs is reached rather
    /// than left to which of two the scheduler happens to start first.
    /// </summary>
    private const int Parallelism = 6;

    /// <summary>The number the master typed in, smaller than the roster below.</summary>
    private const int StatedLimit = 1;

    /// <summary>The attribute specification the NPC below carries a value for.</summary>
    private static readonly Guid NpcAttributeId = Guid.Parse("6c1b0a10-0000-4000-8000-000000000001");

    /// <summary>
    /// How long the queued call is watched before it is called blocked. Long
    /// enough that a call which does not wait at all has finished.
    /// </summary>
    private static readonly TimeSpan Probe = TimeSpan.FromSeconds(2);

    /// <summary>The bound on the whole wait, so a lock nobody releases fails.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    public CharacterPcLimitRacesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RaiseTheStatedLimitWhenApplicationsAreAcceptedAtOnce()
    {
        Guid gameId;
        var characterIds = new List<Guid>();
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            gameId = await AddGameAsync(dbContext);
            // Seeded one at a time: this DbContext is one connection, and the
            // parallelism under test starts below with a scope per caller.
            for (var i = 0; i < Parallelism; i++)
            {
                characterIds.Add(await AddApplicantAsync(dbContext, gameId, i));
            }
        }

        await Task.WhenAll(characterIds.Select(ActivateAsync));

        await TheGameShouldSeatEveryoneItAdmitted(gameId, Parallelism);
    }

    /// <summary>
    /// The same for the other door into the active roster: a master adding
    /// characters that are active from the start.
    /// </summary>
    [Fact]
    public async Task RaiseTheStatedLimitWhenActiveCharactersAreCreatedAtOnce()
    {
        Guid gameId;
        Guid authorId;
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            gameId = await AddGameAsync(dbContext);
            authorId = await AddUserAsync(dbContext);
        }

        await Task.WhenAll(Enumerable
            .Range(0, Parallelism)
            .Select(i => CreateActiveAsync(gameId, authorId, i)));

        await TheGameShouldSeatEveryoneItAdmitted(gameId, Parallelism);
    }

    /// <summary>
    /// An acceptance waits for the acceptance in flight beside it rather than
    /// deciding the limit on a roster that does not yet include it.
    /// </summary>
    /// <remarks>The winner is held open in the repository's shape: game row taken, character already active.</remarks>
    [Fact]
    public async Task WaitForTheAcceptanceInFlightBeforeDecidingTheLimit()
    {
        Guid gameId;
        Guid winnerCharacterId;
        Guid queuedCharacterId;
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            gameId = await AddGameAsync(dbContext);
            winnerCharacterId = await AddApplicantAsync(dbContext, gameId, 0);
            queuedCharacterId = await AddApplicantAsync(dbContext, gameId, 1);
        }

        await using var accepting = DatabaseFixture.CreateDbContext();
        await using var winner = await accepting.Database.BeginTransactionAsync();
        await accepting.Database.ExecuteSqlRawAsync(
            """SELECT "GameId" FROM "Games" WHERE "GameId" = {0} FOR UPDATE""", gameId);
        await accepting.Characters
            .Where(c => c.CharacterId == winnerCharacterId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CharacterStatus.Active));

        var queued = Task.Run(() => ActivateAsync(queuedCharacterId));
        var decidedWhileTheWinnerWasOpen = await Task.WhenAny(queued, Task.Delay(Probe)) == queued;

        await winner.CommitAsync();
        await queued.WaitAsync(Bound);

        decidedWhileTheWinnerWasOpen.Should().BeFalse(
            "the acceptance takes the game row before it reads the roster it decides on, and " +
            "the acceptance in flight holds that row");
        await TheGameShouldSeatEveryoneItAdmitted(gameId, 2);
    }

    /// <summary>
    /// The same ordering for creation: a character added while another is still
    /// uncommitted is counted with it, not instead of it.
    /// </summary>
    /// <remarks>The character in flight is inserted plainly: an insert alone puts somebody on the roster.</remarks>
    [Fact]
    public async Task WaitForTheCharacterInFlightBeforeDecidingTheLimit()
    {
        Guid gameId;
        Guid authorId;
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            gameId = await AddGameAsync(dbContext);
            authorId = await AddUserAsync(dbContext);
        }

        await using var creating = DatabaseFixture.CreateDbContext();
        await using var inFlight = await creating.Database.BeginTransactionAsync();
        creating.Characters.Add(NewCharacter(gameId, authorId, CharacterStatus.Active, "In flight"));
        await creating.SaveChangesAsync();

        var queued = Task.Run(() => CreateActiveAsync(gameId, authorId, 1));
        var decidedWhileTheOtherWasOpen = await Task.WhenAny(queued, Task.Delay(Probe)) == queued;

        await inFlight.CommitAsync();
        await queued.WaitAsync(Bound);

        decidedWhileTheOtherWasOpen.Should().BeFalse(
            "the creation takes the game row before it reads the roster it decides on, and " +
            "the character in flight holds that row through its own foreign key");
        await TheGameShouldSeatEveryoneItAdmitted(gameId, 2);
    }

    /// <summary>
    /// An edit that cannot reach the active roster waits for nobody.
    /// </summary>
    /// <remarks>The game row serializes only what can move the limit; the mirror of the two tests above.</remarks>
    [Fact]
    public async Task EditAnNpcWithoutWaitingForTheAcceptanceInFlight()
    {
        Guid gameId;
        Guid npcId;
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            gameId = await AddGameAsync(dbContext);
            npcId = await AddNpcAsync(dbContext, gameId);
        }

        await using var accepting = DatabaseFixture.CreateDbContext();
        await using var holder = await accepting.Database.BeginTransactionAsync();
        await accepting.Database.ExecuteSqlRawAsync(
            """SELECT "GameId" FROM "Games" WHERE "GameId" = {0} FOR UPDATE""", gameId);

        var edit = Task.Run(() => EditAttributeAsync(npcId, "Отредактировано"));
        var finishedWhileTheGameWasHeld = await Task.WhenAny(edit, Task.Delay(Probe)) == edit;

        await holder.CommitAsync();
        await edit.WaitAsync(Bound);

        finishedWhileTheGameWasHeld.Should().BeTrue(
            "an NPC attribute moves no number the game announces, so the edit has no reason " +
            "to take the game row and none to wait for whoever is holding it");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.CharacterAttributes
                .Where(a => a.CharacterId == npcId)
                .Select(a => a.Value)
                .SingleAsync())
            .Should().Be("Отредактировано", "the edit went through while the game was held");
    }

    private async Task EditAttributeAsync(Guid characterId, string value)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        await repository.Update(new UpdateCharacterEntity
        {
            CharacterId = characterId,
            Attributes = [new CharacterAttributeInput { Id = NpcAttributeId, Value = value }]
        });
    }

    private async Task TheGameShouldSeatEveryoneItAdmitted(Guid gameId, int expectedRoster)
    {
        await using var db = DatabaseFixture.CreateDbContext();

        var activeCount = await db.Characters.CountAsync(c =>
            c.GameId == gameId && !c.IsRemoved && !c.IsNpc && c.Status == CharacterStatus.Active);
        var statedLimit = await db.Games
            .Where(g => g.GameId == gameId)
            .Select(g => g.RecruitmentPcLimit)
            .FirstAsync();

        activeCount.Should().Be(expectedRoster, "every caller put its character on the roster");
        statedLimit.Should().NotBeNull();
        statedLimit!.Value.Should().BeGreaterThanOrEqualTo(activeCount,
            "a game announcing fewer seats than it has players is a state no reader can make " +
            "sense of, and nothing recomputes the number afterwards");
    }

    private async Task ActivateAsync(Guid characterId)
    {
        // One scope per concurrent call: a DbContext is scoped and must not be
        // shared between parallel callers.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        await repository.Update(new UpdateCharacterEntity
        {
            CharacterId = characterId,
            Status = CharacterStatus.Active
        });
    }

    private async Task CreateActiveAsync(Guid gameId, Guid authorId, int index)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        await repository.Create(new CreateCharacterEntity
        {
            CharacterId = Guid.NewGuid(),
            GameId = gameId,
            AuthorId = authorId,
            Name = $"Created at once {index}",
            IsNpc = false,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            InitialStatus = CharacterStatus.Active,
            CreatedUtc = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// A non-player character of the game, with one attribute already stored.
    /// </summary>
    /// <remarks>No foreign key stands behind the attribute identifier.</remarks>
    private static async Task<Guid> AddNpcAsync(DmDbContext dbContext, Guid gameId)
    {
        var authorId = await AddUserAsync(dbContext);
        var npc = NewCharacter(gameId, authorId, CharacterStatus.Active, "NPC");
        npc.IsNpc = true;
        dbContext.Characters.Add(npc);
        dbContext.CharacterAttributes.Add(new DbCharacterAttribute
        {
            CharacterAttributeId = Guid.NewGuid(),
            CharacterId = npc.CharacterId,
            AttributeId = NpcAttributeId,
            Value = "Исходное",
        });
        await dbContext.SaveChangesAsync();
        return npc.CharacterId;
    }

    private static async Task<Guid> AddApplicantAsync(DmDbContext dbContext, Guid gameId, int index)
    {
        // An author of its own per character: two active characters of one player
        // are a different rule, and the roster count this test is about does not
        // distinguish them.
        var authorId = await AddUserAsync(dbContext);
        var character = NewCharacter(gameId, authorId, CharacterStatus.UnderReview, $"Applicant {index}");
        dbContext.Characters.Add(character);
        await dbContext.SaveChangesAsync();
        return character.CharacterId;
    }

    private static DbCharacter NewCharacter(Guid gameId, Guid authorId, CharacterStatus status, string name) =>
        new()
        {
            CharacterId = Guid.NewGuid(),
            GameId = gameId,
            AuthorId = authorId,
            Status = status,
            Name = name,
            IsNpc = false,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = DateTimeOffset.UtcNow,
            IsRemoved = false
        };

    /// <summary>
    /// A user of this test's own: the fixture database is shared and seeded, so a
    /// fixed identifier would depend on whoever else wrote one.
    /// </summary>
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"pcl{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    /// <summary>A recruiting game with the master it needs to exist.</summary>
    private static async Task<Guid> AddGameAsync(DmDbContext dbContext)
    {
        var masterId = await AddUserAsync(dbContext);
        var gameId = Guid.NewGuid();

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the stated limit race check",
            MasterId = masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            RecruitmentPcLimit = StatedLimit,
        });
        await dbContext.SaveChangesAsync();

        return gameId;
    }
}
