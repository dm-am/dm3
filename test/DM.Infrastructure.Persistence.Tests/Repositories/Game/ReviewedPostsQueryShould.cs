using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.Repositories.Game;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

// A feed of reviewed posts keeps a few hundred out of every post on the site, so
// "has a review" is asked of the post row, not of the aggregate the projection computes.
public class ReviewedPostsQueryShould
{
    /// <summary>
    /// The repository over a probe context; building the query reads nothing, and
    /// neither read reaches the game repository before it runs.
    /// </summary>
    private static PostRepository Repository(DmDbContext context) =>
        new(context, null!, new FixedClock(DateTimeOffset.UnixEpoch));

    private static string OuterWhereClause(string sql)
    {
        // The outer WHERE starts at column zero; it runs to the outer ORDER BY or to the end.
        var match = Regex.Match(sql, @"^WHERE .*?(?=^ORDER BY |\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        match.Success.Should().BeTrue("the query is filtered");
        return match.Value;
    }

    private static void AskTheReviewOfThePostRow(string sql)
    {
        var where = OuterWhereClause(sql);

        where.Should().MatchRegex(@"EXISTS \(\s*SELECT 1\s*FROM ""PostReviews""",
            "a post without a live review leaves before its rating, count and last review are computed");
        where.Should().NotContain("count(",
            "counting the reviews of every visible post to compare the count with zero is the cost the filter avoids");
    }

    [Fact]
    public async Task FilterTheRatedFeedBeforeTheRating()
    {
        using var context = ProbeContext.Open();

        var sql = (await Repository(context).RatedPosts(new PostsQuery { HasReviews = true })).ToQueryString();

        AskTheReviewOfThePostRow(sql);
    }

    [Fact]
    public void FilterARoomBeforeTheRating()
    {
        using var context = ProbeContext.Open();

        var sql = Repository(context)
            .RoomPosts(Guid.Empty, new RoomPostsQuery { HasReviews = true }, Guid.Empty, null)
            .ToQueryString();

        AskTheReviewOfThePostRow(sql);
    }
}
