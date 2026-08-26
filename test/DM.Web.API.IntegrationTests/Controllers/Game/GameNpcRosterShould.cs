using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A game whose roster holds an NPC answers with its details, and the NPC line
/// says it has no owner rather than naming one.
/// </summary>
/// <remarks>
/// The roster line used to declare its owner non-nullable while the character
/// row carries no author identifier for an NPC at all. Mapperly answers a null
/// arriving in a member that says it cannot be null by throwing, so the whole
/// response died — every game with a single NPC in it, which on a populated
/// stand is every game with posts, answered 500 to everybody including its own
/// master. The failure was invisible to the mapper tests because none of them
/// mapped an authorless roster line, and invisible to the controller tests
/// because the fixture seeds only player characters.
///
/// The test seeds both kinds into one game on purpose: an NPC alone would pass
/// against a mapper that dropped the owner from every line.
/// </remarks>
public class GameNpcRosterShould : IntegrationTestBase
{
    public GameNpcRosterShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task AnswerTheDetailsOfAGameThatHoldsAnAuthorlessCharacter()
    {
        var seeded = await SeedAsync();

        var response = await Client.GetAsync($"/v1/games/{seeded.PublicId}/details");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an NPC has no author by design, and a roster line without one is not " +
            "a reason to refuse the whole page. Body was: {0}", body);
    }

    [Fact]
    public async Task OmitTheOwnerOfAnNpcAndKeepTheOwnerOfAPlayerCharacter()
    {
        var seeded = await SeedAsync();

        var response = await Client.GetAsync($"/v1/games/{seeded.PublicId}/details");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        var characters = JsonDocument.Parse(body).RootElement
            .GetProperty("resource")
            .GetProperty("characters")
            .EnumerateArray()
            .ToList();

        var npc = characters.Single(c => c.GetProperty("name").GetString() == seeded.NpcName);
        npc.TryGetProperty("author", out _).Should().BeFalse(
            "nulls are dropped on the wire, so an absent owner is spelled by the " +
            "absence of the field - not by a member holding an empty user");

        var played = characters.Single(c => c.GetProperty("name").GetString() == seeded.PlayerCharacterName);
        played.GetProperty("author").GetProperty("username").GetString()
            .Should().Be(seeded.PlayerUsername,
                "a character somebody plays still answers with the player who does");
    }

    private sealed record SeededGame(
        string PublicId,
        string NpcName,
        string PlayerCharacterName,
        string PlayerUsername);

    /// <summary>
    /// One public game holding two roster lines: an NPC with no author
    /// identifier, and a character its player owns. Fresh identifiers every
    /// time - the fixture database is shared and both usernames and game public
    /// identifiers are uniquely indexed.
    /// </summary>
    private async Task<SeededGame> SeedAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var master = AddUser(dbContext, "npcm");
        var player = AddUser(dbContext, "npcp");

        var game = new DbGame
        {
            GameId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = master.UserId,
            Title = "Игра с неигровым персонажем",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Public,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        dbContext.Games.Add(game);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var npcName = "Трактирщик " + suffix;
        var playerCharacterName = "Следопыт " + suffix;

        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = Guid.NewGuid(),
            GameId = game.GameId,
            // The whole point: an NPC is run by the master and owned by nobody.
            AuthorId = null,
            IsNpc = true,
            Status = CharacterStatus.Active,
            Name = npcName,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = Guid.NewGuid(),
            GameId = game.GameId,
            AuthorId = player.UserId,
            IsNpc = false,
            Status = CharacterStatus.Active,
            Name = playerCharacterName,
            CreatedUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        return new SeededGame(game.PublicId!, npcName, playerCharacterName, player.Username);
    }

    private static DbUser AddUser(DmDbContext dbContext, string prefix)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@npc-roster.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        return user;
    }
}
