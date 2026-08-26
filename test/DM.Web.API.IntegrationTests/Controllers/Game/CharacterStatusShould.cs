using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Taking a character out of a game answers over HTTP at all.
/// </summary>
/// <remarks>
/// It did not. Retiring went through PATCH of the whole character with a target
/// status and three booleans; the mapping dropped the booleans, the service had
/// to guess the intent from what arrived, and the combination that arrived
/// matched no arm, so the endpoint answered 500. The request never got that far
/// in practice either — the body requires a privacy block the status-only client
/// call did not send, so it answered 400 first. Every way out of a game was
/// unreachable, and the exile right had no caller at all.
/// </remarks>
public class CharacterStatusShould : IntegrationTestBase
{
    public CharacterStatusShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RetireTheCharacterTheMasterKills()
    {
        var (master, characterId) = await AddGameWithCharacterAsync(CharacterStatus.Active);

        var response = await Send(characterId, CharacterStatusTransition.Kill, master);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var character = await Reload(characterId);
        character.Status.Should().Be(CharacterStatus.Retired);
        character.IsDead.Should().BeTrue("the roster shows why a character left");
        character.IsPlayerLeft.Should().BeFalse();
        character.IsPlayerExiled.Should().BeFalse();
    }

    [Fact]
    public async Task ExileTheCharacterTheMasterThrowsOut()
    {
        var (master, characterId) = await AddGameWithCharacterAsync(CharacterStatus.Active);

        var response = await Send(characterId, CharacterStatusTransition.Exile, master);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var character = await Reload(characterId);
        character.Status.Should().Be(CharacterStatus.Retired);
        character.IsPlayerExiled.Should().BeTrue(
            "exile had no caller before this endpoint existed");
    }

    /// <summary>
    /// Killing something already retired is the caller's mistake, so 400 — not
    /// the 500 an unhandled exception produced.
    /// </summary>
    [Fact]
    public async Task RefuseATransitionThatDoesNotStartFromTheCurrentStatus()
    {
        var (master, characterId) = await AddGameWithCharacterAsync(CharacterStatus.Retired);

        var response = await Send(characterId, CharacterStatusTransition.Kill, master);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefuseAStrangerWhoIsNotInTheGame()
    {
        var (_, characterId) = await AddGameWithCharacterAsync(CharacterStatus.Active);
        var stranger = new GeneralUser
        {
            UserId = Guid.NewGuid(),
            Username = "stranger",
            Role = UserRole.RegularUser,
        };

        var response = await Send(characterId, CharacterStatusTransition.Kill, stranger);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnswerNotFoundForACharacterThatIsNotThere()
    {
        var (master, _) = await AddGameWithCharacterAsync(CharacterStatus.Active);

        var response = await Send(Guid.NewGuid(), CharacterStatusTransition.Kill, master);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private Task<HttpResponseMessage> Send(
        Guid characterId, CharacterStatusTransition transition, GeneralUser actor)
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/characters/{characterId}/status", actor);
        request.Content = JsonContent.Create(new { transition = transition.ToString() });
        return Client.SendAsync(request);
    }

    private async Task<DbCharacter> Reload(Guid characterId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        return await dbContext.Characters.FindAsync(characterId)
               ?? throw new InvalidOperationException("character disappeared");
    }

    /// <summary>
    /// A fresh master, game and character per test: the fixture database is shared
    /// and seeded, so fixed identifiers would collide.
    /// </summary>
    private async Task<(GeneralUser Master, Guid CharacterId)> AddGameWithCharacterAsync(
        CharacterStatus status)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var masterId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();

        foreach (var (id, prefix) in new[] { (masterId, "mst"), (playerId, "ply") })
        {
            dbContext.Users.Add(new DbUser
            {
                UserId = id,
                // Username is varchar(20) and uniquely indexed, so the id is truncated.
                Username = $"{prefix}{id:N}"[..20],
                Email = $"{id:N}@status.example",
                PasswordHash = "hash",
                Salt = "salt",
            });
        }

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the character status check",
            MasterId = masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = gameId,
            AuthorId = playerId,
            Status = status,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync();

        var master = dbContext.Users.Find(masterId)!;
        return (new GeneralUser
        {
            UserId = masterId,
            Username = master.Username,
            Role = UserRole.RegularUser,
        }, characterId);
    }
}
