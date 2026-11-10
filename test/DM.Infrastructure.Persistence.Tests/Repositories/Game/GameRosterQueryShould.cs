using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Repositories.Game;
using AwesomeAssertions;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAssistant = DM.Infrastructure.Persistence.Entities.Game.Links.GameAssistant;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

// Readers are deduplicated against everyone on the list, nobody else is: an assistant who plays
// holds two roles and his character hangs off the second.
public class GameRosterQueryShould
{
    private static readonly DateTimeOffset Created = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid GameId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000001");
    private static readonly Guid MasterId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000002");
    private static readonly Guid AssistantId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000003");
    private static readonly Guid PlayerId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000004");
    private static readonly Guid ReaderId = Guid.Parse("6b1d0f11-0000-4000-8000-000000000005");

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmDbContext Context() => InMemoryStore.Open(_databaseName);

    private static DbCharacter NewCharacter(Guid authorId, string name) => new()
    {
        CharacterId = Guid.NewGuid(),
        GameId = GameId,
        AuthorId = authorId,
        Name = name,
        Status = CharacterStatus.Active,
        CreatedUtc = Created,
    };

    private DmDbContext Seeded()
    {
        var context = Context();
        context.Users.AddRange(
            InMemoryStore.User(MasterId, "master"),
            InMemoryStore.User(AssistantId, "assistant"),
            InMemoryStore.User(PlayerId, "player"),
            InMemoryStore.User(ReaderId, "reader"));
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
