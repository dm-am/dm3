using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A character holds one value per attribute specification, and that was true
/// in code only. The update path reads the stored rows into a dictionary keyed
/// by AttributeId and writes them as check-then-insert, so a second row for the
/// same pair — from a body that repeated an identifier or from two saves racing
/// on the same new attribute — made every later edit of that character fail on
/// the duplicate key, permanently and with no way back short of deleting the
/// row by hand.
/// </summary>
public class CharacterAttributeUniquenessShould : IntegrationTestBase
{
    public CharacterAttributeUniquenessShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RejectASecondValueForTheSameSpecification()
    {
        var (characterId, attributeId) = await AddCharacterWithAttributeAsync();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var insertDuplicate = async () => await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "CharacterAttributes" ("CharacterAttributeId", "CharacterId", "AttributeId", "Value")
            VALUES ({0}, {1}, {2}, 'duplicate')
            """,
            Guid.NewGuid(), characterId, attributeId);

        (await insertDuplicate.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("IX_CharacterAttributes_CharacterId_AttributeId");
    }

    /// <summary>
    /// The pair is what has to be unique: another specification on the same
    /// character, and the same specification on another character, are both
    /// ordinary rows.
    /// </summary>
    [Fact]
    public async Task KeepValuesOfDifferentSpecificationsAndCharacters()
    {
        var (characterId, attributeId) = await AddCharacterWithAttributeAsync();
        var (otherCharacterId, _) = await AddCharacterWithAttributeAsync();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.CharacterAttributes.AddRange(
            new DbCharacterAttribute
            {
                CharacterAttributeId = Guid.NewGuid(),
                CharacterId = characterId,
                AttributeId = Guid.NewGuid(),
                Value = "another specification",
            },
            new DbCharacterAttribute
            {
                CharacterAttributeId = Guid.NewGuid(),
                CharacterId = otherCharacterId,
                AttributeId = attributeId,
                Value = "another character",
            });

        var save = async () => await dbContext.SaveChangesAsync();
        await save.Should().NotThrowAsync();
    }

    /// <summary>
    /// A fresh master, game, character and attribute row per call: the fixture
    /// database is shared and seeded, so fixed identifiers would collide.
    /// </summary>
    private async Task<(Guid CharacterId, Guid AttributeId)> AddCharacterWithAttributeAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var attributeId = Guid.NewGuid();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is truncated.
            Username = $"atr{userId:N}"[..20],
            Email = $"{userId:N}@attributes.example",
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the attribute uniqueness check",
            MasterId = userId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = gameId,
            AuthorId = userId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        dbContext.CharacterAttributes.Add(new DbCharacterAttribute
        {
            CharacterAttributeId = Guid.NewGuid(),
            CharacterId = characterId,
            AttributeId = attributeId,
            Value = "stored",
        });

        await dbContext.SaveChangesAsync();
        return (characterId, attributeId);
    }
}
