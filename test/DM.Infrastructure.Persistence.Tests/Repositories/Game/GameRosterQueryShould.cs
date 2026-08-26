using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Repositories.Game;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAssistant = DM.Infrastructure.Persistence.Entities.Game.Links.GameAssistant;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

/// <summary>
/// The roster is a list of roles, and one of them is deduplicated.
/// </summary>
/// <remarks>
/// Four places used to derive "what is this user to this game", and the roster
/// query is the one nothing checked. It deduplicates readers against everyone
/// already on the list and deduplicates nobody else, which reads like an
/// oversight and is a decision: a subscription is implied by every other role, so
/// a second row for it would say nothing, while an assistant who plays holds two
/// roles and his character hangs off the second one. Collapsing those two rows
/// would have to drop either the staff role or the character.
///
/// Written against the query rather than against the service because that is
/// where both halves live, and neither of them is visible to the architecture
/// rules: they see types and references, not a Where.
/// </remarks>
public class GameRosterQueryShould : UnitTestBase
{
    private static readonly DateTimeOffset Created = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid GameId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000001");
    private static readonly Guid MasterId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000002");
    private static readonly Guid AssistantId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000003");
    private static readonly Guid PlayerId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000004");
    private static readonly Guid ReaderId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000005");

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(_databaseName)
        .Options);

    private static DbUser NewUser(Guid id, string username) => new()
    {
        UserId = id,
        Username = username,
        Email = $"{username}@test.local",
        Salt = "",
        PasswordHash = "",
    };

    private static DbCharacter NewCharacter(Guid authorId, string name) => new()
    {
        CharacterId = Guid.NewGuid(),
        GameId = GameId,
        AuthorId = authorId,
        Name = name,
        Status = CharacterStatus.Active,
        CreatedUtc = Created,
    };

    /// <summary>
    /// A master, an assistant who also plays, a player with two active characters
    /// and a reader who is also that player.
    /// </summary>
    private DmDbContext Seeded()
    {
        var context = Context();
        context.Users.AddRange(
            NewUser(MasterId, "master"),
            NewUser(AssistantId, "assistant"),
            NewUser(PlayerId, "player"),
            NewUser(ReaderId, "reader"));
        context.Games.Add(new DbGame
        {
            GameId = GameId,
            PublicId = "aaaaa",
            MasterId = MasterId,
            Title = "Игра",
            CreatedUtc = Created,
        });
        context.Set<DbGameAssistant>().Add(new DbGameAssistant
        {
            GameAssistantId = Guid.NewGuid(),
            GameId = GameId,
            UserId = AssistantId,
            JoinedUtc = Created,
        });
        context.Set<DbCharacter>().AddRange(
            NewCharacter(AssistantId, "Тень"),
            NewCharacter(PlayerId, "Гром"),
            NewCharacter(PlayerId, "Искра"));
        context.Set<DbSubscription>().AddRange(
            new DbSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                SubscriberId = ReaderId,
                TargetType = SubscriptionTargetType.Game,
                TargetId = GameId,
                CreatedUtc = Created,
            },
            new DbSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                SubscriberId = PlayerId,
                TargetType = SubscriptionTargetType.Game,
                TargetId = GameId,
                CreatedUtc = Created,
            });
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task WriteNoReaderRowForSomebodyAlreadyOnTheRoster()
    {
        using var context = Seeded();

        var users = (await new GameInvitationRepository(context).GetUsers(GameId)).ToArray();

        users.Where(u => u.Role == GameRole.Reader)
            .Should().ContainSingle().Which.User.UserId.Should().Be(ReaderId,
                "a subscription is implied by every other role, so it is written only for a user who has no other");
    }

    [Fact]
    public async Task WriteARowPerCharacterForAPlayerWhoHasTwo()
    {
        using var context = Seeded();

        var users = (await new GameInvitationRepository(context).GetUsers(GameId)).ToArray();

        users.Where(u => u.User.UserId == PlayerId && u.Role == GameRole.Player)
            .Select(u => u.CharacterName)
            .Should().BeEquivalentTo(["Гром", "Искра"],
                "the character hangs off the row, so one row per person would hide the second character");
    }

    [Fact]
    public async Task WriteBothRowsForAnAssistantWhoAlsoPlays()
    {
        using var context = Seeded();

        var users = (await new GameInvitationRepository(context).GetUsers(GameId)).ToArray();
        var his = users.Where(u => u.User.UserId == AssistantId).ToArray();

        his.Select(u => u.Role).Should().BeEquivalentTo(
            [GameRole.Assistant, GameRole.Player],
            "an entry is a role and not a person: dropping either row drops a fact the roster is there to state");
        his.Single(u => u.Role == GameRole.Player).CharacterName.Should().Be("Тень");
    }
}
