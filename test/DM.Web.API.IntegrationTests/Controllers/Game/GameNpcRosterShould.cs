using System.Net;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

using DM.Web.API.IntegrationTests.Helpers;

using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <remarks>
/// The character row holds no author for an NPC, and Mapperly throws on a null into a non-nullable member. Both
/// kinds are seeded into one game: an NPC alone passes a mapper that drops every owner.
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
        dbContext.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(game.GameId));

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

    private static DbUser AddUser(DmDbContext dbContext, string prefix) =>
        UserRows.Add(dbContext, prefix, "npc-roster.example");
}
