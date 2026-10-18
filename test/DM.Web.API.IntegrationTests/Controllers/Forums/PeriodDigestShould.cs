using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Digests;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// Two instances generating the digest of one period leave one topic between
/// them, and the board counts what it holds.
/// </summary>
/// <remarks>
/// The period used to be claimed after the topic had been created, so the run
/// that lost the race was left holding a topic and had to take it back. It could
/// not: the board points at its last topic through a foreign key that refuses the
/// delete, and the board's topic count had already been raised for a topic that
/// was supposed to stop existing. Taking the period first turns the race into a
/// question asked before anything is written, and the loser writes nothing at all.
///
/// Two hosts is the shape this happens in - the digest runs on a schedule in
/// every instance - and only a live database can be asked which of two writers
/// won a unique index.
/// </remarks>
public class PeriodDigestShould : IntegrationTestBase
{
    /// <summary>The board the digests are posted to, seeded by the migration.</summary>
    private static readonly Guid NewsBoardId = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    public PeriodDigestShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CreateOneTopicWhenTwoInstancesGenerateThePeriodAtOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var lastMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1);
        var periods = new (int Year, int? Month)[]
        {
            (lastMonth.Year, lastMonth.Month),
            (now.Year - 1, null),
        };

        // The fixture database is shared and a previous run may have generated
        // these two already, in which case both callers below would find the
        // periods taken and the test would assert over nothing.
        await DropDigests(periods);
        try
        {
            var runs = await Task.WhenAll(GeneratedTitlesAsync(), GeneratedTitlesAsync());

            runs.Sum(titles => titles.Length).Should().Be(periods.Length,
                "the two periods are generated once between the two runs, and the run that " +
                "lost a period reports nothing for it");

            await using var db = DatabaseFixture.CreateDbContext();
            foreach (var (year, month) in periods)
            {
                var markers = await db.PeriodDigestTopics
                    .Where(d => d.Year == year && d.Month == month)
                    .Select(d => d.TopicId)
                    .ToListAsync();

                markers.Should().HaveCount(1, "a period is claimed once");
                (await db.Topics.CountAsync(t => t.TopicId == markers[0]))
                    .Should().Be(1, "the marker names the topic the winner created");
            }

            var board = await db.Boards.AsNoTracking().FirstAsync(b => b.BoardId == NewsBoardId);
            var topicsOnTheBoard = await db.Topics.CountAsync(t => t.BoardId == NewsBoardId);

            board.TopicsCount.Should().Be(topicsOnTheBoard,
                "the loser created no topic, so there is none for the count to be carrying");
        }
        finally
        {
            await DropDigests(periods);
        }
    }

    /// <summary>
    /// A period whose topic was never written goes back on the shelf.
    /// </summary>
    /// <remarks>
    /// The claim is one committed statement of its own, taken before the topic
    /// exists, so a creation that refuses used to leave the period marked as
    /// generated with nothing behind the marker: every later pass read it as
    /// "already there", and a monthly or yearly digest is generated once and never
    /// again. Only a live database answers whether the release actually frees the
    /// partial unique index the next claim runs into.
    /// </remarks>
    [Fact]
    public async Task LetThePeriodBeClaimedAgainAfterAReleasedClaim()
    {
        // Far outside the two periods a real pass looks at, so the digest hosted
        // service cannot take this one out from under the test.
        const int year = 1990;
        int? month = 7;
        var periods = new[] { (Year: year, Month: month) };

        await DropDigests(periods);
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPeriodDigestRepository>();
            var now = DateTimeOffset.UtcNow;
            var abandonedTopicId = Guid.NewGuid();

            (await repository.TryClaimPeriod(abandonedTopicId, year, month, now))
                .Should().BeTrue("nobody holds this period");
            (await repository.DigestExists(year, month))
                .Should().BeTrue("a claim is what makes the period look generated");

            await repository.ReleasePeriod(abandonedTopicId);

            (await repository.DigestExists(year, month)).Should().BeFalse(
                "a claim whose topic was never written must stop answering for the period");
            (await repository.TryClaimPeriod(Guid.NewGuid(), year, month, now)).Should().BeTrue(
                "the next pass makes an honest second attempt at the same period");
        }
        finally
        {
            await DropDigests(periods);
        }
    }

    /// <summary>
    /// A period whose topic did get written stays claimed, whatever the caller asks.
    /// </summary>
    /// <remarks>
    /// Creation commits the topic and then reads it back outside the retry, so a
    /// refusal can reach the caller with the digest already on the board. Giving
    /// the period back there would put a second copy of the same digest up on the
    /// next pass, which is the loss the release exists to prevent, only upside
    /// down. The condition therefore lives in the delete statement rather than in
    /// a read before it.
    /// </remarks>
    [Fact]
    public async Task KeepThePeriodClaimedWhenItsTopicWasWrittenAfterAll()
    {
        const int year = 1991;
        int? month = 8;
        var periods = new[] { (Year: year, Month: month) };

        await DropDigests(periods);
        var topicId = Guid.NewGuid();
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPeriodDigestRepository>();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var now = DateTimeOffset.UtcNow;

            (await repository.TryClaimPeriod(topicId, year, month, now))
                .Should().BeTrue("nobody holds this period");

            dbContext.Topics.Add(new DbTopic
            {
                TopicId = topicId,
                BoardId = NewsBoardId,
                TopicNumber = 990001,
                AuthorId = TestConstants.TestUserId,
                Title = "Итоги августа 1991",
                Text = string.Empty,
                CreatedUtc = now.UtcDateTime,
            });
            await dbContext.SaveChangesAsync();

            (await repository.ReleasePeriod(topicId)).Should().Be(0,
                "the digest is already on the board, and a released period would post it twice");
            (await repository.DigestExists(year, month)).Should().BeTrue(
                "the period stays generated for as long as its topic stands");
        }
        finally
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            await dbContext.Topics.IgnoreQueryFilters()
                .Where(t => t.TopicId == topicId)
                .ExecuteDeleteAsync();
            await DropDigests(periods);
        }
    }

    private async Task<string[]> GeneratedTitlesAsync()
    {
        // One scope per concurrent run: a DbContext is scoped and must not be
        // shared between parallel callers.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IPeriodDigestProcessor>();
        var created = await processor.EnsureClosedPeriodsAsync();
        return created.ToArray();
    }

    /// <summary>
    /// The markers of these periods and the topics they name, out of the way.
    /// </summary>
    /// <remarks>
    /// The board names its last topic through a foreign key, so the reference is
    /// released before the rows it points at can go - the same order the board
    /// helpers of this project clean up in.
    /// </remarks>
    private async Task DropDigests((int Year, int? Month)[] periods)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        foreach (var (year, month) in periods)
        {
            var topicIds = await db.PeriodDigestTopics
                .Where(d => d.Year == year && d.Month == month)
                .Select(d => d.TopicId)
                .ToListAsync();

            await db.PeriodDigestTopics
                .Where(d => d.Year == year && d.Month == month)
                .ExecuteDeleteAsync();

            if (topicIds.Count == 0) continue;

            await db.Boards
                .Where(b => b.LastTopicId != null && topicIds.Contains(b.LastTopicId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.LastTopicId, (Guid?)null));
            await db.Comments.IgnoreQueryFilters()
                .Where(c => topicIds.Contains(c.EntityId))
                .ExecuteDeleteAsync();
            await db.Topics.IgnoreQueryFilters()
                .Where(t => topicIds.Contains(t.TopicId))
                .ExecuteDeleteAsync();
        }

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var topics = scope.ServiceProvider
            .GetRequiredService<DM.Domain.Forum.Features.Topics.ITopicRepository>();
        await topics.RefreshBoardSummary(NewsBoardId);
    }
}
