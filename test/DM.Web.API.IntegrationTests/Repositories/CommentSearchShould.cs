using DM.Domain.Core.Comments;
using DM.Domain.Forum.Features.Comments;

using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Web.API.IntegrationTests.Repositories;

// The search of a discussion reads "%" and "_" as themselves, as every other search of the site
// does.
public class CommentSearchShould : IntegrationTestBase
{
    public CommentSearchShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData("скидка 50%", "скидка 500 рублей")]
    [InlineData("код a_b", "код aXb")]
    public async Task ReadTheSignsOfTheQueryAsThemselves(string literal, string lookalike)
    {
        // A discussion of its own: the fixture database is shared.
        var topicId = Guid.NewGuid();
        await using (var dbContext = DatabaseFixture.CreateDbContext())
        {
            dbContext.Set<DbComment>().AddRange(Comment(topicId, literal), Comment(topicId, lookalike));
            await dbContext.SaveChangesAsync();
        }

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicCommentRepository>();
        (await repository.Count(topicId, new CommentsQuery { Search = literal }))
            .Should().Be(1, "the sign in the query is a character of the text, not a wildcard");
    }

    private static DbComment Comment(Guid entityId, string text) => new()
    {
        CommentId = Guid.NewGuid(),
        EntityId = entityId,
        AuthorId = TestConstants.TestUserId,
        Text = text,
        CreatedUtc = DateTimeOffset.UtcNow,
    };
}
