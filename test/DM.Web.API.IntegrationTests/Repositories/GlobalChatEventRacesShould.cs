using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGlobalChatEvent = DM.Infrastructure.Persistence.Entities.Messaging.GlobalChatEvent;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

// The rules sit in the schema: a unique pair, a partial unique index over the live status, ON
// CONFLICT DO NOTHING and a transition carrying its start status.
public class GlobalChatEventRacesShould : IntegrationTestBase
{
    // Enough callers that the interleaving the defect needs is reached rather than left to which of
    // two the scheduler happens to start first.
    private const int Parallelism = 8;

    // A moment with no sub-microsecond part, so it survives the round trip.
    private static readonly DateTimeOffset Moment =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public GlobalChatEventRacesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // A join that wrote nothing must not tell the chat somebody arrived.
    [Fact]
    public async Task SeatOnePersonOnceHoweverManyClicksArrive()
    {
        var eventId = await SeedEvent(GlobalChatEventStatus.Scheduled);
        var joinerId = await SeedUser();
        try
        {
            var answers = await Task.WhenAll(Enumerable
                .Range(0, Parallelism)
                .Select(_ => JoinAsync(eventId, joinerId)));

            answers.Count(seated => seated).Should().Be(1,
                "one click seats the person and the rest are told the seat is theirs already");

            await using var db = DatabaseFixture.CreateDbContext();
            (await db.GlobalChatEventParticipants
                    .CountAsync(p => p.GlobalChatEventId == eventId && p.UserId == joinerId))
                .Should().Be(1,
                    "a second row is invisible to the reader: the list shows the name once " +
                    "and leaving used to delete only the row that was read back");
        }
        finally
        {
            await RemoveEvents(eventId);
        }
    }

    // The delete is over the pair, and the unique index is about the list as it stands.
    [Fact]
    public async Task LetThePersonBackAfterTheyLeave()
    {
        var eventId = await SeedEvent(GlobalChatEventStatus.Scheduled);
        var joinerId = await SeedUser();
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGlobalChatEventRepository>();

            (await repository.AddParticipant(Participant(eventId, joinerId))).Should().NotBeNull();
            await repository.RemoveParticipant(eventId, joinerId);

            await using (var afterLeaving = DatabaseFixture.CreateDbContext())
            {
                (await afterLeaving.GlobalChatEventParticipants
                        .CountAsync(p => p.GlobalChatEventId == eventId && p.UserId == joinerId))
                    .Should().Be(0, "the person pressed leave");
            }

            (await repository.AddParticipant(Participant(eventId, joinerId)))
                .Should().NotBeNull("somebody who left may come back");
        }
        finally
        {
            await RemoveEvents(eventId);
        }
    }

    // The partial unique index refuses the second, and the repository answers with the read's
    // sentence.
    [Fact]
    public async Task LeaveOneLiveEventWhenTwoOfThemStartTogether()
    {
        var first = await SeedEvent(GlobalChatEventStatus.Scheduled);
        var second = await SeedEvent(GlobalChatEventStatus.Scheduled);
        try
        {
            var answers = await Task.WhenAll(
                StartAsync(first, Moment),
                StartAsync(second, Moment));

            answers.Count(answer => answer == StartOutcome.Started).Should().Be(1,
                "the chat is one room: two events live in it at once leave the strip over " +
                "it naming one of them and a closed event deciding who may write");
            answers.Count(answer => answer == StartOutcome.Refused).Should().Be(1,
                "the loser is refused, not broken: a unique violation reaching the client " +
                "as a 500 is strictly worse than the duplicate it prevents");

            await using var db = DatabaseFixture.CreateDbContext();
            (await db.GlobalChatEvents.CountAsync(e =>
                    (e.GlobalChatEventId == first || e.GlobalChatEventId == second) &&
                    e.Status == GlobalChatEventStatus.Live))
                .Should().Be(1);
        }
        finally
        {
            await RemoveEvents(first, second);
        }
    }

    // The announcement and the stored moment are the one start that moved the event.
    [Fact]
    public async Task MoveTheEventInExactlyOneOfTheStartsThatMeet()
    {
        var eventId = await SeedEvent(GlobalChatEventStatus.Scheduled);
        try
        {
            // A moment of its own per caller, so the row says which one wrote it.
            var attempts = Enumerable.Range(0, Parallelism)
                .Select(index => Moment.AddSeconds(index))
                .ToList();

            var answers = await Task.WhenAll(attempts
                .Select(async moment => (Moment: moment, Outcome: await StartAsync(eventId, moment))));

            var winners = answers.Where(answer => answer.Outcome == StartOutcome.Started).ToList();
            winners.Should().HaveCount(1,
                "the transition carries the status it starts from, so it matches once");

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.GlobalChatEvents.AsNoTracking()
                .FirstAsync(e => e.GlobalChatEventId == eventId);
            stored.Status.Should().Be(GlobalChatEventStatus.Live);
            stored.StartedUtc.Should().Be(winners[0].Moment,
                "the event began when it began; the starts that moved nothing wrote nothing");
        }
        finally
        {
            await RemoveEvents(eventId);
        }
    }

    private enum StartOutcome
    {
        Started,

        Missed,

        Refused
    }

    private async Task<StartOutcome> StartAsync(Guid eventId, DateTimeOffset startedAt)
    {
        // One scope per concurrent call: a DbContext is scoped and must not be
        // shared between parallel callers.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGlobalChatEventRepository>();
        try
        {
            var moved = await repository.UpdateStatus(
                eventId,
                GlobalChatEventStatus.Scheduled,
                GlobalChatEventStatus.Live,
                startedAt,
                endedAt: null);
            return moved == null ? StartOutcome.Missed : StartOutcome.Started;
        }
        catch (DuplicateEntityException)
        {
            return StartOutcome.Refused;
        }
    }

    private async Task<bool> JoinAsync(Guid eventId, Guid userId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGlobalChatEventRepository>();
        return await repository.AddParticipant(Participant(eventId, userId)) != null;
    }

    private static CreateGlobalChatEventParticipantEntity Participant(Guid eventId, Guid userId) => new()
    {
        GlobalChatEventParticipantId = Guid.NewGuid(),
        GlobalChatEventId = eventId,
        UserId = userId,
        IsOrganizer = false,
        JoinedUtc = Moment
    };

    private async Task<Guid> SeedEvent(GlobalChatEventStatus status)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var organizerId = await AddUserAsync(db);
        var eventId = Guid.NewGuid();

        db.GlobalChatEvents.Add(new DbGlobalChatEvent
        {
            GlobalChatEventId = eventId,
            Title = "Эвент для проверки гонок",
            Description = "",
            StartsUtc = Moment,
            IsOpen = true,
            Status = status,
            CreatedByUserId = organizerId,
            CreatedUtc = Moment
        });
        await db.SaveChangesAsync();

        return eventId;
    }

    private async Task<Guid> SeedUser()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await AddUserAsync(db);
    }

    // A user of this test's own: the fixture database is shared and seeded, so a fixed identifier
    // would depend on whoever else wrote one.
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"gce{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    // Takes the test's events back out: one left live is a second live event to the next test.
    private async Task RemoveEvents(params Guid[] eventIds)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.GlobalChatEventParticipants
            .Where(p => eventIds.Contains(p.GlobalChatEventId))
            .ExecuteDeleteAsync();
        await db.GlobalChatEvents
            .Where(e => eventIds.Contains(e.GlobalChatEventId))
            .ExecuteDeleteAsync();
    }
}
