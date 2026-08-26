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
using DbPollVote = DM.Infrastructure.Persistence.Entities.Community.PollVote;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// One voter, one option, and the write is what says so.
/// </summary>
/// <remarks>
/// The primary key of PollVotes is (PollId, UserId) and the vote is
/// INSERT ... ON CONFLICT DO NOTHING: a check before the write reads, decides
/// and writes, and two requests arriving together both read a ballot without
/// this voter on it. Asserted against a live Postgres because the key and the
/// conflict clause are the only place the rule runs at all — the domain tests
/// mock the repository and see nothing. The foreign keys are INV-2: a vote by
/// a voter, for an option or in a poll that does not exist is unrepresentable.
/// </remarks>
public class PollVotingShould : IntegrationTestBase
{
    public PollVotingShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseASecondOptionToTheSameVoter()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPollRepository>();
        var voter = await SeedVoter(scope);
        var (pollId, first, second) = await SeedTwoOptionPoll(scope);

        var voted = await repository.Vote(pollId, first, voter);
        var again = await repository.Vote(pollId, second, voter);

        voted.Should().NotBeNull("the first vote is an ordinary vote");
        again.Should().BeNull("the ballot already carries this voter");

        var stored = await Poll(scope, pollId);
        stored.Options.Single(o => o.PollOptionId == first).Votes.Select(v => v.UserId)
            .Should().Contain(voter);
        stored.Options.Single(o => o.PollOptionId == second).Votes.Select(v => v.UserId)
            .Should().NotContain(voter,
                "a poll where one account holds every option counts nothing");
    }

    [Fact]
    public async Task LetTheVoterBackAfterTheyTakeTheirVoteOff()
    {
        // Changing one's mind is Unvote and then Vote: the endpoint for it
        // exists, and the two together are what a moved vote is made of.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPollRepository>();
        var voter = await SeedVoter(scope);
        var (pollId, first, second) = await SeedTwoOptionPoll(scope);

        await repository.Vote(pollId, first, voter);
        await repository.Unvote(pollId, voter);
        var moved = await repository.Vote(pollId, second, voter);

        moved.Should().NotBeNull();
        var stored = await Poll(scope, pollId);
        stored.Options.Single(o => o.PollOptionId == first).Votes.Select(v => v.UserId)
            .Should().NotContain(voter);
        stored.Options.Single(o => o.PollOptionId == second).Votes.Select(v => v.UserId)
            .Should().Contain(voter);
    }

    [Fact]
    public async Task CountEveryVoterSeparately()
    {
        // The rule is about one voter, not about the poll: a second person voting
        // for the same option is the ordinary case and must not be refused.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPollRepository>();
        var first = await SeedVoter(scope);
        var second = await SeedVoter(scope);
        var (pollId, optionId, _) = await SeedTwoOptionPoll(scope);

        await repository.Vote(pollId, optionId, first);
        var secondVote = await repository.Vote(pollId, optionId, second);

        secondVote.Should().NotBeNull();
        var stored = await Poll(scope, pollId);
        stored.Options.Single(o => o.PollOptionId == optionId).Votes.Select(v => v.UserId)
            .Should().BeEquivalentTo(new[] { first, second });
    }

    /// <summary>
    /// INV-2: the voter is a foreign key. A ghost vote — one pointing at a user
    /// the database does not hold — is refused by the constraint, which is what
    /// buried the seeder's self-healing block.
    /// </summary>
    [Fact]
    public async Task RefuseAVoteOfAVoterWhoDoesNotExist()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var (pollId, optionId, _) = await SeedTwoOptionPoll(scope);

        dbContext.PollVotes.Add(new DbPollVote
        {
            PollId = pollId,
            UserId = Guid.NewGuid(),
            PollOptionId = optionId,
            VotedUtc = DateTimeOffset.UtcNow
        });
        var act = () => dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "a vote without a voter is the state the seeder used to repair by hand");
    }

    private static async Task<Guid> SeedVoter(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var voterId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = voterId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"v{voterId:N}"[..20],
            Email = $"{voterId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return voterId;
    }

    private static async Task<(Guid PollId, Guid First, Guid Second)> SeedTwoOptionPoll(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var pollId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        dbContext.Polls.Add(new DbPoll
        {
            PollId = pollId,
            Title = "Кто ведет игру?",
            StartsUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndsUtc = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            Options =
            [
                new DbPollOption { PollOptionId = first, PollId = pollId, Text = "Первый", Order = 0 },
                new DbPollOption { PollOptionId = second, PollId = pollId, Text = "Второй", Order = 1 }
            ]
        });
        await dbContext.SaveChangesAsync();

        return (pollId, first, second);
    }

    private static Task<DbPoll> Poll(IServiceScope scope, Guid pollId) =>
        scope.ServiceProvider.GetRequiredService<DmDbContext>().Polls
            .Include(p => p.Options)
            .ThenInclude(o => o.Votes)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstAsync(p => p.PollId == pollId);
}
