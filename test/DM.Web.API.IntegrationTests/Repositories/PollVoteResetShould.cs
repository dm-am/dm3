using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Polls;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbPoll = DM.Infrastructure.Persistence.Entities.Community.Poll;
using DbPollOption = DM.Infrastructure.Persistence.Entities.Community.PollOption;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Publishing an anonymous poll clears its ballot, and a vote cast at that
/// moment is on one side of the clearing or the other, never inside it.
/// </summary>
/// <remarks>
/// The reset used to read the flag it decides on with no lock and delete the
/// votes it happened to see, while a vote was a bare insert that waited for
/// nothing: a vote committed a moment later outlived the reset and appeared, by
/// name, on a ballot its voter had cast anonymously. The poll row is what
/// serializes the two now - the reset holds it exclusively, the vote reads it
/// shared - and only a live server can be asked whether one write waits for
/// another, which is why this is here and not in the domain tests.
///
/// Each test commits its hand-held transaction and awaits the queued call before
/// asserting: the waiting call must not be left holding a scope the test is
/// tearing down, and a bounded wait fails the run instead of hanging it.
/// </remarks>
public class PollVoteResetShould : IntegrationTestBase
{
    /// <summary>
    /// How long the queued call is watched before it is called blocked. Long
    /// enough that a call which does not wait at all has finished; short enough
    /// that it is paid once per test.
    /// </summary>
    private static readonly TimeSpan Probe = TimeSpan.FromSeconds(2);

    /// <summary>The bound on the whole wait, so a lock nobody releases fails.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    public PollVoteResetShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// A vote still in flight when the ballot is cleared is cleared with it,
    /// rather than surfacing afterwards on the published poll.
    /// </summary>
    /// <remarks>
    /// The vote is held open by hand, in the shape the repository casts one in,
    /// because a vote committed a moment later is not the interesting ordering:
    /// what broke the promise is a vote that is invisible to the reset's delete
    /// and committed before the reset's own commit. The reset here is the real
    /// call, so the ordering of its lock against its read is what is under test.
    /// </remarks>
    [Fact]
    public async Task ClearTheVoteThatWasStillInFlightWhenTheBallotWasCleared()
    {
        using var seedScope = DatabaseFixture.Factory.Services.CreateScope();
        var (pollId, optionId) = await SeedAnonymousPoll(seedScope);
        var earlyVoter = await SeedVoter(seedScope);
        var lateVoter = await SeedVoter(seedScope);

        using (var earlyScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var earlyRepository = earlyScope.ServiceProvider.GetRequiredService<IPollRepository>();
            (await earlyRepository.Vote(pollId, optionId, earlyVoter)).Should().NotBeNull();
        }

        await using var voting = DatabaseFixture.CreateDbContext();
        await using var vote = await voting.Database.BeginTransactionAsync();
        await voting.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "PollVotes" ("PollId", "UserId", "PollOptionId", "VotedUtc")
            SELECT {0}, {1}, {2}, now()
            FROM "Polls"
            WHERE "PollId" = {0}
            FOR SHARE
            ON CONFLICT ("PollId", "UserId") DO NOTHING
            """,
            pollId, lateVoter, optionId);

        using var editScope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = editScope.ServiceProvider.GetRequiredService<IPollRepository>();
        var reset = Task.Run(() => repository.Update(pollId, null, null, null, null, false));

        var decidedWhileTheVoteWasOpen = await Task.WhenAny(reset, Task.Delay(Probe)) == reset;

        await vote.CommitAsync();
        var published = await reset.WaitAsync(Bound);

        decidedWhileTheVoteWasOpen.Should().BeFalse(
            "the reset takes the poll row before it reads the flag it decides on, and the " +
            "vote in flight holds that row");
        published.IsAnonymous.Should().BeFalse();

        await using var db = DatabaseFixture.CreateDbContext();
        var voters = await db.PollVotes.Where(v => v.PollId == pollId)
            .Select(v => v.UserId).ToListAsync();
        voters.Should().BeEmpty(
            "every vote cast while the poll was anonymous is cleared by publishing it, " +
            "including the one that had not committed when the reset started");
    }

    /// <summary>
    /// A poll is published once. A second request to publish it, sent while the
    /// first is still in flight, must not clear the ballot again.
    /// </summary>
    /// <remarks>
    /// The vote left on the table stands for a vote that belongs on the public
    /// ballot: the poll is public by the time the second request gets to decide,
    /// and clearing then throws away votes nobody cast anonymously. The decision
    /// used to be made on a read taken before the transaction was even opened,
    /// which is a read that cannot see the request in flight.
    /// </remarks>
    [Fact]
    public async Task LeaveTheBallotAloneWhenThePollIsPublicByTheTimeItDecides()
    {
        using var seedScope = DatabaseFixture.Factory.Services.CreateScope();
        var (pollId, optionId) = await SeedAnonymousPoll(seedScope);
        var voter = await SeedVoter(seedScope);

        using (var votingScope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var votingRepository = votingScope.ServiceProvider.GetRequiredService<IPollRepository>();
            (await votingRepository.Vote(pollId, optionId, voter)).Should().NotBeNull();
        }

        // The request that won the race, held open by hand: the poll row taken
        // and the flag already flipped to public.
        await using var publishing = DatabaseFixture.CreateDbContext();
        await using var publish = await publishing.Database.BeginTransactionAsync();
        await publishing.Database.ExecuteSqlRawAsync(
            """SELECT "IsAnonymous" FROM "Polls" WHERE "PollId" = {0} FOR UPDATE""", pollId);
        await publishing.Polls.Where(p => p.PollId == pollId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsAnonymous, false));

        using var editScope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = editScope.ServiceProvider.GetRequiredService<IPollRepository>();
        var second = Task.Run(() => repository.Update(pollId, null, null, null, null, false));

        await Task.WhenAny(second, Task.Delay(Probe));

        await publish.CommitAsync();
        var edited = await second.WaitAsync(Bound);

        edited.IsAnonymous.Should().BeFalse();

        await using var db = DatabaseFixture.CreateDbContext();
        var voters = await db.PollVotes.Where(v => v.PollId == pollId)
            .Select(v => v.UserId).ToListAsync();
        voters.Should().BeEquivalentTo(new[] { voter },
            "the poll was already public when the second request decided, so there was " +
            "nothing for it to reset");
    }

    private static async Task<Guid> SeedVoter(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var voterId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = voterId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"pr{voterId:N}"[..20],
            Email = $"{voterId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return voterId;
    }

    private static async Task<(Guid PollId, Guid OptionId)> SeedAnonymousPoll(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();

        dbContext.Polls.Add(new DbPoll
        {
            PollId = pollId,
            Title = "Кто ведет игру?",
            IsAnonymous = true,
            StartsUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndsUtc = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            Options =
            [
                new DbPollOption { PollOptionId = optionId, PollId = pollId, Text = "Первый", Order = 0 }
            ]
        });
        await dbContext.SaveChangesAsync();

        return (pollId, optionId);
    }
}
