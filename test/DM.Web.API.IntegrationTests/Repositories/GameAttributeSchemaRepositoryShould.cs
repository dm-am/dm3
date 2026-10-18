using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The character sheet of a game: written with the game, replaced under a
/// version, and taking the values it drops with it.
/// </summary>
/// <remarks>
/// Against a live Postgres, because every claim here is one the database
/// settles: the sheet and the game land in one save or neither does, the
/// conditional replacement is a predicate on the row, and the values it deletes
/// belong to characters the ordinary reads cannot even see.
/// </remarks>
public class GameAttributeSchemaRepositoryShould : IntegrationTestBase
{
    public GameAttributeSchemaRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The game and its sheet are one write, and the identifiers of the
    /// specifications are the server's.
    /// </summary>
    [Fact]
    public async Task WriteTheSheetInTheSameSaveAsTheGame()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var masterId = await SeedUser(dbContext);
        var gameId = Guid.NewGuid();

        await repository.Create(
            CreateGame(gameId, masterId, new CreateAttributeSpecification
            {
                Title = "Раса",
                Type = AttributeSpecificationType.Text,
                Order = 0
            }),
            CreateRoom(gameId));

        var stored = await dbContext.Set<DbGameAttributeSchema>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GameId == gameId);

        stored.Should().NotBeNull("a game without a sheet is a state no read has to survive");
        stored!.Version.Should().Be(0, "the sheet a creation leaves behind has been replaced by nobody");
        var specification = stored.Specifications.Should().ContainSingle().Subject;
        specification.Title.Should().Be("Раса");
        specification.Id.Should().NotBeEmpty(
            "the server hands every specification of a new sheet an identifier of its own");
    }

    /// <summary>
    /// A game whose sheet row is missing is a defect of whoever wrote the game,
    /// and the read says so instead of answering with a sheet nobody chose.
    /// </summary>
    [Fact]
    public async Task FailLoudlyForAGameWithoutASheet()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var masterId = await SeedUser(dbContext);
        var gameId = await SeedGameWithoutSheet(dbContext, masterId);

        var read = async () => await repository.Get(gameId);

        await read.Should().ThrowAsync<InvalidOperationException>(
            "an empty sheet would hide every value of every character of that game");
    }

    /// <summary>
    /// A replacement aimed at a game with no sheet row fails the way a read of
    /// it does, rather than coming back empty-handed like a lost version.
    /// </summary>
    /// <remarks>
    /// The two are not the same answer. A lost version tells the caller to look
    /// again and there is a row to look at; a missing sheet is a defect of
    /// whoever wrote the game, and sending that caller off to reload would hide
    /// it behind a conflict nobody can resolve.
    /// </remarks>
    [Fact]
    public async Task FailLoudlyWhenReplacingTheSheetOfAGameThatHasNone()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var masterId = await SeedUser(dbContext);
        var gameId = await SeedGameWithoutSheet(dbContext, masterId);

        var replace = async () => await repository.Replace(new ReplaceGameAttributeSchema
        {
            GameId = gameId,
            Version = 0,
            Specifications = [Specification("Раса", null, 0)]
        });

        await replace.Should().ThrowAsync<InvalidOperationException>(
            "an empty answer is reserved for a version somebody else moved");
    }

    /// <summary>
    /// A specification keeps its identifier only when the stored sheet holds
    /// it; an empty, absent or invented one is replaced.
    /// </summary>
    [Fact]
    public async Task KeepOnlyTheIdentifiersTheStoredSheetHolds()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameAttributeSchemaRepository>();

        var (gameId, keptId) = await SeedGameWithOneSpecification(scope);
        var inventedId = Guid.NewGuid();

        var replaced = await repository.Replace(new ReplaceGameAttributeSchema
        {
            GameId = gameId,
            Version = 0,
            Specifications =
            [
                Specification("Раса", keptId, 0),
                Specification("Класс", inventedId, 1),
                Specification("Возраст", Guid.Empty, 2),
                Specification("Рост", null, 3)
            ]
        });

        replaced.Should().NotBeNull();
        var specifications = replaced!.Specifications.ToList();
        specifications.Single(s => s.Title == "Раса").Id.Should().Be(keptId,
            "the values written against it answer to this identifier");
        specifications.Single(s => s.Title == "Класс").Id.Should().NotBe(inventedId,
            "an identifier the sheet never held is a caller's invention");
        specifications.Single(s => s.Title == "Возраст").Id.Should().NotBeEmpty();
        specifications.Single(s => s.Title == "Рост").Id.Should().NotBeEmpty();
        specifications.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        replaced.Version.Should().Be(1, "a written replacement moves the version by one");
    }

    /// <summary>
    /// The values of every character of the game go with the specifications the
    /// new set no longer names, soft-deleted characters included.
    /// </summary>
    [Fact]
    public async Task DropTheValuesOfEveryCharacterOutsideTheNewSet()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var (gameId, droppedId) = await SeedGameWithOneSpecification(scope);
        var liveCharacterId = await SeedCharacter(dbContext, gameId, isRemoved: false);
        var removedCharacterId = await SeedCharacter(dbContext, gameId, isRemoved: true);
        await SeedValue(dbContext, liveCharacterId, droppedId, "живой");
        await SeedValue(dbContext, removedCharacterId, droppedId, "удаленный");

        var replaced = await repository.Replace(new ReplaceGameAttributeSchema
        {
            GameId = gameId,
            Version = 0,
            Specifications = [Specification("Класс", null, 0)]
        });

        replaced.Should().NotBeNull();
        var survivors = await dbContext.CharacterAttributes
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.AttributeId == droppedId)
            .ToListAsync();

        survivors.Should().BeEmpty(
            "a value whose specification is gone is unreadable, and a soft-deleted character " +
            "hides its rows from every read that could have collected them later");
    }

    /// <summary>
    /// A replacement carrying a version somebody else has already moved writes
    /// nothing at all - not the document, and not the deletion of values.
    /// </summary>
    [Fact]
    public async Task RefuseAReplacementOnAVersionThatMovedOn()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var (gameId, specificationId) = await SeedGameWithOneSpecification(scope);
        var characterId = await SeedCharacter(dbContext, gameId, isRemoved: false);
        await SeedValue(dbContext, characterId, specificationId, "сохранить");

        var first = await repository.Replace(new ReplaceGameAttributeSchema
        {
            GameId = gameId,
            Version = 0,
            Specifications = [Specification("Раса", specificationId, 0)]
        });
        first.Should().NotBeNull();

        var second = await repository.Replace(new ReplaceGameAttributeSchema
        {
            GameId = gameId,
            Version = 0,
            Specifications = [Specification("Класс", null, 0)]
        });

        second.Should().BeNull("the version the caller read is not the version that is stored");

        var stored = await repository.Get(gameId);
        stored.Version.Should().Be(1, "the refused replacement moved nothing");
        stored.Specifications.Should().ContainSingle().Which.Title.Should().Be("Раса");
        (await dbContext.CharacterAttributes
            .AsNoTracking()
            .CountAsync(a => a.AttributeId == specificationId))
            .Should().Be(1, "a refused replacement deletes no values either");
    }

    /// <summary>
    /// A value written after a replacement deleted its row is stored, not
    /// refused: it becomes one orphan until the next replacement collects it.
    /// </summary>
    /// <remarks>
    /// This is the losing side of the race the write path is shaped around. The
    /// player read a sheet, the sheet was replaced, and the save that follows
    /// must not answer them with a failure over a value they were entitled to
    /// write when they started.
    /// </remarks>
    [Fact]
    public async Task StoreAValueWrittenAfterItsRowWasDeleted()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameAttributeSchemaRepository>();
        var characterRepository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var (gameId, droppedId) = await SeedGameWithOneSpecification(scope);
        var characterId = await SeedCharacter(dbContext, gameId, isRemoved: false);
        await SeedValue(dbContext, characterId, droppedId, "прежнее");

        await repository.Replace(new ReplaceGameAttributeSchema
        {
            GameId = gameId,
            Version = 0,
            Specifications = [Specification("Класс", null, 0)]
        });

        var write = async () => await characterRepository.Update(new UpdateCharacterEntity
        {
            CharacterId = characterId,
            Attributes = [new CharacterAttributeInput { Id = droppedId, Value = "позднее" }]
        });

        await write.Should().NotThrowAsync(
            "the player is not refused for a sheet somebody else replaced under them");

        var orphan = await dbContext.CharacterAttributes
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.CharacterId == characterId && a.AttributeId == droppedId);
        orphan.Should().NotBeNull();
        orphan!.Value.Should().Be("позднее");
    }

    private static UpdateAttributeSpecification Specification(string title, Guid? id, int order) => new()
    {
        Id = id,
        Title = title,
        Type = AttributeSpecificationType.Text,
        Order = order
    };

    private static CreateGameEntity CreateGame(
        Guid gameId, Guid masterId, params CreateAttributeSpecification[] specifications) => new()
        {
            GameId = gameId,
            MasterId = masterId,
            Title = "Игра со своей анкетой",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = "Игра для проверки анкеты.",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            AttributeSpecifications = specifications,
            CreatedUtc = DateTimeOffset.UtcNow
        };

    private static CreateRoomEntity CreateRoom(Guid gameId) => new()
    {
        RoomId = Guid.NewGuid(),
        GameId = gameId,
        Title = "Игровая",
        Type = RoomType.Default,
        AccessType = RoomAccessType.Open,
        ViewDiceResults = true,
        DiceEnabled = true
    };

    /// <summary>
    /// A game whose sheet holds one text specification, at version zero.
    /// </summary>
    private static async Task<(Guid GameId, Guid SpecificationId)> SeedGameWithOneSpecification(
        IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var schemas = scope.ServiceProvider.GetRequiredService<IGameAttributeSchemaRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var masterId = await SeedUser(dbContext);
        var gameId = Guid.NewGuid();
        await repository.Create(
            CreateGame(gameId, masterId, new CreateAttributeSpecification
            {
                Title = "Раса",
                Type = AttributeSpecificationType.Text,
                Order = 0
            }),
            CreateRoom(gameId));

        var sheet = await schemas.Get(gameId);
        return (gameId, sheet.Specifications.Single().Id);
    }

    private static async Task<Guid> SeedGameWithoutSheet(DmDbContext dbContext, Guid masterId)
    {
        var gameId = Guid.NewGuid();
        dbContext.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = masterId,
            Title = "Игра без анкеты",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return gameId;
    }

    private static async Task<Guid> SeedCharacter(DmDbContext dbContext, Guid gameId, bool isRemoved)
    {
        var characterId = Guid.NewGuid();
        dbContext.Set<DbCharacter>().Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = gameId,
            Status = CharacterStatus.Active,
            Name = "Персонаж " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
            IsRemoved = isRemoved
        });
        await dbContext.SaveChangesAsync();
        return characterId;
    }

    private static async Task SeedValue(
        DmDbContext dbContext, Guid characterId, Guid attributeId, string value)
    {
        dbContext.CharacterAttributes.Add(new DbCharacterAttribute
        {
            CharacterAttributeId = Guid.NewGuid(),
            CharacterId = characterId,
            AttributeId = attributeId,
            Value = value
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> SeedUser(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"g{userId:N}"[..20],
            Email = $"{userId:N}@sheets.example",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }
}
