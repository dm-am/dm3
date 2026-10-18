using System;
using System.Linq;
using System.Threading.Tasks;
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

/// <summary>
/// One person is on one event's list once, and one event is live at a time.
/// </summary>
/// <remarks>
/// Both rules used to be held by a read taken before the write: "is this person a
/// participant" and "is any event live". Under READ COMMITTED neither read sees
/// the row the other transaction has not committed yet, so two clicks arriving
/// together both passed and both wrote. The duplicate participant was not even
/// something the reader could clear - leaving deleted the one row a read returned
/// - and two live events left the chat belonging to both at once.
///
/// The rules now sit in the schema: a unique pair, a partial unique index over
/// the live status, an insert that says ON CONFLICT DO NOTHING and a transition
/// that carries the status it starts from. Only a live database can be asked
/// whether a second write is refused or made to wait, which is why these are here
/// and not in the domain tests.
/// </remarks>
public class GlobalChatEventRacesShould : IntegrationTestBase
{
    /// <summary>
    /// Enough callers that the interleaving the defect needs is reached rather
    /// than left to which of two the scheduler happens to start first.
    /// </summary>
    private const int Parallelism = 8;

    /// <summary>A moment with no sub-microsecond part, so it survives the round trip.</summary>
    private static readonly DateTimeOffset Moment =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public GlobalChatEventRacesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Simultaneous joins of one person leave one row, and one of them answers
    /// that it wrote it.
    /// </summary>
    /// <remarks>
    /// The answer is what the service builds its refusal and its announcement on:
    /// a join that wrote nothing must not tell the chat somebody arrived.
    /// </remarks>
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

    /// <summary>
    /// Leaving leaves, and the person can join again afterwards.
    /// </summary>
    /// <remarks>
    /// The delete is a statement over the pair rather than over a row read first,
    /// so what it clears is everything the pair holds. Joining again has to be
    /// allowed: the unique index is about the list as it stands, not about who has
    /// ever been on it.
    /// </remarks>
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

    /// <summary>
    /// Two events starting at the same moment leave one live event, and the other
    /// start is refused rather than answered with a 500.
    /// </summary>
    /// <remarks>
    /// The check before the write is a read and cannot see the event going live
    /// beside it. What refuses the second one is the partial unique index, and the
    /// repository turns its violation into the duplicate the service answers with:
    /// the same sentence the read answers with, because to the organiser it is the
    /// same fact.
    /// </remarks>
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

    /// <summary>
    /// Of the starts that meet on one event, exactly one moves it, and the moment
    /// stored is that one's.
    /// </summary>
    /// <remarks>
    /// The announcement stands on the answer: a start that moved nothing and
    /// announced anyway tells every reader of the chat the event began as many
    /// times as the button was pressed. The stamp is the other half - the event
    /// began once, and a later start writing its own moment over the first would
    /// move the countdown the client draws from it.
    /// </remarks>
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

    /// <summary>What one call to start an event can come back with.</summary>
    private enum StartOutcome
    {
        /// <summary>This call moved the event into the live status.</summary>
        Started,

        /// <summary>The event was no longer the scheduled one this call read.</summary>
        Missed,

        /// <summary>Another event holds the live status.</summary>
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

    /// <summary>
    /// An event of this test's own, with the organiser it needs to exist.
    /// </summary>
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
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"gce{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    /// <summary>
    /// Takes the test's events back out. The database is shared by every test in
    /// the collection, and one live event left behind is a second live event to
    /// whoever runs next - which is the very state these tests are about.
    /// </summary>
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
