using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Polls;
using DM.Infrastructure.Persistence.MongoIntegration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using DbPoll = DM.Infrastructure.Persistence.Entities.Community.Poll;
using DbPollOption = DM.Infrastructure.Persistence.Entities.Community.PollOption;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// One voter, one option, and the write is what says so.
/// </summary>
/// <remarks>
/// Voting added the voter to the chosen option and looked nowhere else: no layer
/// of the stack asked whether they had already voted, so one account could take
/// every option in a poll and the result meant nothing.
///
/// The rule belongs in the filter rather than in a check before the write. A
/// check reads, decides, and writes, and two requests arriving together both
/// read a ballot without this voter on it. Asserted against a live Mongo because
/// that is the only place the filter runs at all: the domain tests mock the
/// repository and see nothing.
/// </remarks>
public class PollVotingShould : IntegrationTestBase
{
    public PollVotingShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseASecondOptionToTheSameVoter()
    {
        var voter = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPollRepository>();
        var (pollId, first, second) = await SeedTwoOptionPoll(scope);

        var voted = await repository.Vote(pollId, first, voter);
        var again = await repository.Vote(pollId, second, voter);

        voted.Should().NotBeNull("the first vote is an ordinary vote");
        again.Should().BeNull("the ballot already carries this voter");

        var stored = await Poll(scope, pollId);
        stored.Options.Single(o => o.Id == first).UserIds.Should().Contain(voter);
        stored.Options.Single(o => o.Id == second).UserIds.Should().NotContain(voter,
            "a poll where one account holds every option counts nothing");
    }

    [Fact]
    public async Task LetTheVoterBackAfterTheyTakeTheirVoteOff()
    {
        // Changing one's mind is Unvote and then Vote: the endpoint for it
        // exists, and the two together are what a moved vote is made of.
        var voter = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPollRepository>();
        var (pollId, first, second) = await SeedTwoOptionPoll(scope);

        await repository.Vote(pollId, first, voter);
        await repository.Unvote(pollId, voter);
        var moved = await repository.Vote(pollId, second, voter);

        moved.Should().NotBeNull();
        var stored = await Poll(scope, pollId);
        stored.Options.Single(o => o.Id == first).UserIds.Should().NotContain(voter);
        stored.Options.Single(o => o.Id == second).UserIds.Should().Contain(voter);
    }

    [Fact]
    public async Task CountEveryVoterSeparately()
    {
        // The rule is about one voter, not about the poll: a second person voting
        // for the same option is the ordinary case and must not be refused.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPollRepository>();
        var (pollId, optionId, _) = await SeedTwoOptionPoll(scope);

        await repository.Vote(pollId, optionId, first);
        var secondVote = await repository.Vote(pollId, optionId, second);

        secondVote.Should().NotBeNull();
        var stored = await Poll(scope, pollId);
        stored.Options.Single(o => o.Id == optionId).UserIds
            .Should().BeEquivalentTo(new[] { first, second });
    }

    private static async Task<(Guid PollId, Guid First, Guid Second)> SeedTwoOptionPoll(IServiceScope scope)
    {
        var pollId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await Collection(scope).InsertOneAsync(new DbPoll
        {
            Id = pollId,
            Title = "Кто ведет игру?",
            StartsUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndsUtc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Options = new List<DbPollOption>
            {
                new() { Id = first, Text = "Первый", UserIds = [] },
                new() { Id = second, Text = "Второй", UserIds = [] }
            }
        });

        return (pollId, first, second);
    }

    private static IMongoCollection<DbPoll> Collection(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmMongoClient>().GetCollection<DbPoll>();

    private static Task<DbPoll> Poll(IServiceScope scope, Guid pollId) =>
        Collection(scope).Find(Builders<DbPoll>.Filter.Eq(p => p.Id, pollId)).FirstAsync();
}
