using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAssistant = DM.Infrastructure.Persistence.Entities.Game.Links.GameAssistant;
using DbToken = DM.Infrastructure.Persistence.Entities.Account.Token;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

// Under READ COMMITTED neither the kill nor the "already an assistant" read sees the other
// transaction's row.
public class GameInvitationRacesShould : IntegrationTestBase
{
    // Enough callers that the interleaving the defect needs is reached rather than left to which of
    // two the scheduler happens to start first.
    private const int Parallelism = 8;

    // How long the queued call is watched before it is called blocked. Long enough that a call
    // which does not wait at all has finished.
    private static readonly TimeSpan Probe = TimeSpan.FromSeconds(2);

    // The bound on the whole wait, so a lock nobody releases fails.
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    public GameInvitationRacesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task LeaveOneLiveTokenAfterConcurrentInvitationsOfOnePerson()
    {
        Guid masterId;
        Guid gameId;
        Guid invitedId;
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            (masterId, gameId) = await AddGameAsync(dbContext);
            invitedId = await AddUserAsync(dbContext);
        }

        await Task.WhenAll(Enumerable
            .Range(0, Parallelism)
            .Select(_ => InviteAsync(gameId, masterId, invitedId)));

        await using var db = DatabaseFixture.CreateDbContext();

        (await db.Tokens.CountAsync(t =>
                t.EntityId == gameId &&
                t.UserId == invitedId &&
                t.Type == TokenType.GamePlayerInvitation &&
                !t.IsRemoved))
            .Should().Be(1,
                "an invitation replaces the one before it, and a link the master cannot see " +
                "on the roster still opens the game to whoever holds it");

        (await db.Tokens.IgnoreQueryFilters().CountAsync(t =>
                t.EntityId == gameId &&
                t.UserId == invitedId &&
                t.Type == TokenType.GamePlayerInvitation))
            .Should().Be(Parallelism,
                "every caller issued its own token; that all but one of them are dead is what " +
                "the count above is about");
    }

    /// <remarks>
    /// The winner is held open by hand in the repository's shape and the accept is the real call. The invitation is
    /// spent either way: it authorises exactly the state the loser found.
    /// </remarks>
    [Fact]
    public async Task SpendTheInvitationWhoseAssistantRowWasTakenBesideIt()
    {
        Guid masterId;
        Guid gameId;
        Guid assistantId;
        var tokenId = Guid.NewGuid();
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            (masterId, gameId) = await AddGameAsync(dbContext);
            assistantId = await AddUserAsync(dbContext);
            dbContext.Tokens.Add(new DbToken
            {
                TokenId = tokenId,
                UserId = assistantId,
                EntityId = gameId,
                CreatorId = masterId,
                Type = TokenType.GameAssistantInvitation,
                CreatedUtc = DateTimeOffset.UtcNow,
                IsRemoved = false
            });
            await dbContext.SaveChangesAsync();
        }

        // The accept that won the race, held open by hand: the pair inserted and
        // not yet committed, so the call below reads no assistant and inserts one.
        await using var winning = DatabaseFixture.CreateDbContext();
        await using var winner = await winning.Database.BeginTransactionAsync();
        winning.GameAssistants.Add(new DbGameAssistant
        {
            GameAssistantId = Guid.NewGuid(),
            GameId = gameId,
            UserId = assistantId,
            JoinedUtc = DateTimeOffset.UtcNow
        });
        await winning.SaveChangesAsync();

        using var acceptScope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = acceptScope.ServiceProvider.GetRequiredService<IGameInvitationRepository>();
        var accept = Task.Run(() => repository.AcceptAssistantInvitation(
            new AddAssistantEntity
            {
                GameAssistantId = Guid.NewGuid(),
                GameId = gameId,
                UserId = assistantId,
                JoinedUtc = DateTimeOffset.UtcNow
            },
            tokenId));

        var decidedWhileTheWinnerWasOpen = await Task.WhenAny(accept, Task.Delay(Probe)) == accept;

        await winner.CommitAsync();
        var completing = async () => await accept.WaitAsync(Bound);

        await completing.Should().NotThrowAsync(
            "the state the invitation authorises is already in place, which is the outcome " +
            "the person following the link asked for");
        decidedWhileTheWinnerWasOpen.Should().BeFalse(
            "the unique index on the pair makes the second insert wait for the first to " +
            "settle, which is the ordering this test is about");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Tokens.CountAsync(t => t.TokenId == tokenId && !t.IsRemoved))
            .Should().Be(0, "an invitation that has been acted on is spent");
        (await db.GameAssistants.CountAsync(a => a.GameId == gameId && a.UserId == assistantId))
            .Should().Be(1, "a person assists a game once");
    }

    // The event carries the token, so only the acceptance that spent the link announces it.
    [Fact]
    public async Task SpendTheInvitationInExactlyOneOfTheAcceptancesThatMeet()
    {
        Guid gameId;
        Guid assistantId;
        var tokenId = Guid.NewGuid();
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            var (masterId, seededGameId) = await AddGameAsync(dbContext);
            gameId = seededGameId;
            assistantId = await AddUserAsync(dbContext);
            dbContext.Tokens.Add(new DbToken
            {
                TokenId = tokenId,
                UserId = assistantId,
                EntityId = gameId,
                CreatorId = masterId,
                Type = TokenType.GameAssistantInvitation,
                CreatedUtc = DateTimeOffset.UtcNow,
                IsRemoved = false
            });
            await dbContext.SaveChangesAsync();
        }

        var answers = await Task.WhenAll(Enumerable
            .Range(0, Parallelism)
            .Select(_ => AcceptAsync(gameId, assistantId, tokenId)));

        answers.Count(spent => spent).Should().Be(1,
            "the link is spent once, so exactly one caller has an acceptance to announce");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Tokens.CountAsync(t => t.TokenId == tokenId && !t.IsRemoved))
            .Should().Be(0, "an invitation that has been acted on is spent");
        (await db.GameAssistants.CountAsync(a => a.GameId == gameId && a.UserId == assistantId))
            .Should().Be(1, "a person assists a game once");
    }

    /// <remarks>
    /// The revocation is held open by hand to settle between the "still live" read and the spend, where the acceptance
    /// has written a seat and finds no link to spend for it.
    /// </remarks>
    [Fact]
    public async Task SeatNobodyByAnInvitationRevokedBesideTheAcceptance()
    {
        Guid masterId;
        Guid gameId;
        Guid assistantId;
        var tokenId = Guid.NewGuid();
        using (var seedScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<DmDbContext>();
            (masterId, gameId) = await AddGameAsync(dbContext);
            assistantId = await AddUserAsync(dbContext);
            dbContext.Tokens.Add(new DbToken
            {
                TokenId = tokenId,
                UserId = assistantId,
                EntityId = gameId,
                CreatorId = masterId,
                Type = TokenType.GameAssistantInvitation,
                CreatedUtc = DateTimeOffset.UtcNow,
                IsRemoved = false
            });
            await dbContext.SaveChangesAsync();
        }

        // The master revoking the link, held open by hand: the row is taken and
        // still live to everybody else, so the accept below reads a live link and
        // then queues on this very row.
        await using var revoking = DatabaseFixture.CreateDbContext();
        await using var revocation = await revoking.Database.BeginTransactionAsync();
        await revoking.Database.ExecuteSqlRawAsync(
            """UPDATE "Tokens" SET "IsRemoved" = true WHERE "TokenId" = {0}""", tokenId);

        var accept = Task.Run(() => AcceptAsync(gameId, assistantId, tokenId));
        var decidedWhileTheRevocationWasOpen = await Task.WhenAny(accept, Task.Delay(Probe)) == accept;

        await revocation.CommitAsync();
        var spent = await accept.WaitAsync(Bound);

        decidedWhileTheRevocationWasOpen.Should().BeFalse(
            "the link is spent by a statement over the row the revocation holds, which is " +
            "the ordering this test is about");
        spent.Should().BeFalse("there was no live link left for this call to spend");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.GameAssistants.CountAsync(a => a.GameId == gameId && a.UserId == assistantId))
            .Should().Be(0,
                "the seat stands on the link that grants it, and the master had taken that " +
                "link back before this acceptance reached it");
        (await db.Tokens.CountAsync(t => t.TokenId == tokenId && !t.IsRemoved))
            .Should().Be(0, "the revocation stands");
    }

    private async Task<bool> AcceptAsync(Guid gameId, Guid assistantId, Guid tokenId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameInvitationRepository>();
        return await repository.AcceptAssistantInvitation(new AddAssistantEntity
        {
            GameAssistantId = Guid.NewGuid(),
            GameId = gameId,
            UserId = assistantId,
            JoinedUtc = DateTimeOffset.UtcNow
        }, tokenId);
    }

    private async Task InviteAsync(Guid gameId, Guid masterId, Guid invitedId)
    {
        // One scope per concurrent call: a DbContext is scoped and must not be
        // shared between parallel callers.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameInvitationRepository>();
        await repository.InvalidateAndCreateInvitation(new CreateGameInvitationEntity
        {
            TokenId = Guid.NewGuid(),
            GameId = gameId,
            UserId = invitedId,
            CreatorId = masterId,
            TokenType = TokenType.GamePlayerInvitation,
            CreatedUtc = DateTimeOffset.UtcNow
        });
    }

    // A user of this test's own: the fixture database is shared and seeded, so a fixed identifier
    // would depend on whoever else wrote one.
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"inv{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static async Task<(Guid MasterId, Guid GameId)> AddGameAsync(DmDbContext dbContext)
    {
        var masterId = await AddUserAsync(dbContext);
        var gameId = Guid.NewGuid();

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the invitation race check",
            MasterId = masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        await dbContext.SaveChangesAsync();

        return (masterId, gameId);
    }
}
