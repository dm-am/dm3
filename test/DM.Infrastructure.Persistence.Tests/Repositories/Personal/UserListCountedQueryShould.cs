using System;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Infrastructure.Persistence.Repositories.Personal;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Personal;

// A correlated count runs per row of Users before OFFSET.
public class UserListCountedQueryShould
{

    private static IQueryable<DbUser> Users(DmDbContext context) =>
        context.Users.Where(u => !u.IsRemoved);

    private static string OrderByClause(string sql)
    {
        var index = sql.LastIndexOf("ORDER BY", StringComparison.Ordinal);
        index.Should().BeGreaterThan(-1, "the query is sorted");
        return sql[index..];
    }

    private static string OuterWhereClause(string sql)
    {
        // The outer WHERE is the one at column zero of its line: everything a
        // derived table contains is indented by the generator.
        var match = Regex.Match(sql, @"^WHERE .*$", RegexOptions.Multiline);
        match.Success.Should().BeTrue("the query is filtered");
        return match.Value;
    }

    private static string SortedByGamesHosting(DmDbContext context) =>
        UserCountQueries.Order(
                UserCountQueries.WithCountSum(Users(context),
                    UserCountQueries.GamesMastered(context), UserCountQueries.GamesAssisted(context)),
                false)
            .Skip(0).Take(50).Select(x => x.User).ToQueryString();

    private static string SortedByBlogsHosting(DmDbContext context) =>
        UserCountQueries.Order(
                UserCountQueries.WithCountSum(Users(context),
                    UserCountQueries.BlogsOwned(context), UserCountQueries.BlogsAssisted(context)),
                false)
            .Skip(0).Take(50).Select(x => x.User).ToQueryString();

    private static string SortedByGamesPlaying(DmDbContext context) =>
        UserCountQueries.Order(
                UserCountQueries.WithCount(Users(context), UserCountQueries.GamesPlayed(context)), true)
            .Skip(0).Take(50).Select(x => x.User).ToQueryString();

    private static string SortedByPopularity(DmDbContext context) =>
        UserCountQueries.Order(
                UserCountQueries.WithCount(Users(context),
                    UserCountQueries.ActiveSubscribers(context, DateTimeOffset.UnixEpoch)), false)
            .Skip(0).Take(50).Select(x => x.User).ToQueryString();

    [Theory]
    [InlineData(nameof(SortedByGamesHosting))]
    [InlineData(nameof(SortedByBlogsHosting))]
    [InlineData(nameof(SortedByGamesPlaying))]
    [InlineData(nameof(SortedByPopularity))]
    public void SortByAColumnAndNotByASubquery(string sortName)
    {
        using var context = ProbeContext.Open();

        var sql = sortName switch
        {
            nameof(SortedByGamesHosting) => SortedByGamesHosting(context),
            nameof(SortedByBlogsHosting) => SortedByBlogsHosting(context),
            nameof(SortedByGamesPlaying) => SortedByGamesPlaying(context),
            _ => SortedByPopularity(context),
        };

        OrderByClause(sql).Should().NotContain("SELECT",
            "a SELECT in the ORDER BY is a count correlated to the user row, and Postgres runs it " +
            "for every registered user before OFFSET");
    }

    [Theory]
    [InlineData(nameof(SortedByGamesHosting))]
    [InlineData(nameof(SortedByBlogsHosting))]
    [InlineData(nameof(SortedByGamesPlaying))]
    [InlineData(nameof(SortedByPopularity))]
    public void CountOnceInADerivedTableOfItsOwn(string sortName)
    {
        using var context = ProbeContext.Open();

        var sql = sortName switch
        {
            nameof(SortedByGamesHosting) => SortedByGamesHosting(context),
            nameof(SortedByBlogsHosting) => SortedByBlogsHosting(context),
            nameof(SortedByGamesPlaying) => SortedByGamesPlaying(context),
            _ => SortedByPopularity(context),
        };

        sql.Should().Contain("LEFT JOIN", "the counter is joined to the page, not recomputed per row");
        sql.Should().Contain("GROUP BY", "the counter is one aggregate over the counted table");
        sql.Should().Contain("COALESCE", "a user the counter has no row for counts zero, as he did before");
    }

    [Fact]
    public void PageAtTheOutermostLevelSoTheOrderSurvives()
    {
        using var context = ProbeContext.Open();

        var sql = SortedByGamesHosting(context);

        // LIMIT after ORDER BY in the same statement: were the ordered query a derived
        // table with the paging outside it, the row order would be whatever the plan
        // happened to produce.
        sql.LastIndexOf("LIMIT", StringComparison.Ordinal).Should().BeGreaterThan(
            sql.LastIndexOf("ORDER BY", StringComparison.Ordinal),
            "the page is taken from the ordered rows, not ordered after being taken");
    }

    /// <summary>
    /// The list repository over a probe context; <see cref="UserRepository.BuildPageQuery" /> never touches the mapper.
    /// </summary>
    private static UserRepository Repository(DmDbContext context) =>
        new(context, new FixedClock(DateTimeOffset.UnixEpoch));

    private static PagingData FirstPage() => new(new PagingQuery { Skip = 0, Take = 50 }, 50, 1000);

    private static int Occurrences(string sql, string clause) =>
        Regex.Matches(sql, Regex.Escape(clause)).Count;

    // Read off the repository, which decides the joins a request ends up with.
    [Theory]
    [InlineData(UserSort.GamesHosting, "GamesHosting")]
    [InlineData(UserSort.BlogsHosting, "BlogsHosting")]
    [InlineData(UserSort.GamesPlaying, "GamesPlaying")]
    public void CountTheSameTableOnceWhenTheRangeAndTheSortShareIt(UserSort sort, string counter)
    {
        using var context = ProbeContext.Open();

        var filter = new UserFilter { Sort = sort, SortAscending = false };
        filter = counter switch
        {
            "GamesHosting" => filter with { MinGamesHosting = 1 },
            "BlogsHosting" => filter with { MinBlogsHosting = 1 },
            _ => filter with { MinGamesPlaying = 1 },
        };

        var sql = Repository(context).BuildPageQuery(FirstPage(), filter).ToQueryString();

        // One GROUP BY per counted table: hosting sums two tables, playing counts one.
        var expected = counter == "GamesPlaying" ? 1 : 2;
        Occurrences(sql, "GROUP BY").Should().Be(expected,
            "the range and the sort read one number, so they share the one join — a second " +
            "aggregate over the same table is a second pass over it for the same page");
        Occurrences(sql, "LEFT JOIN").Should().Be(expected,
            "one left join per counted table, and the range is a predicate over what it brought");
    }

    // Paired by counter: two different numbers are two joins.
    [Fact]
    public void KeepTheJoinsApartWhenTheRangeAndTheSortReadDifferentNumbers()
    {
        using var context = ProbeContext.Open();

        var sql = Repository(context).BuildPageQuery(FirstPage(), new UserFilter
        {
            Sort = UserSort.GamesHosting,
            MinGamesPlaying = 1,
        }).ToQueryString();

        Occurrences(sql, "GROUP BY").Should().Be(3,
            "games played is one aggregate and games hosted is two, and neither answers for the other");
    }

    [Fact]
    public void FilterByAColumnAndNotByASubquery()
    {
        using var context = ProbeContext.Open();

        var sql = UserCountQueries.InRange(
                UserCountQueries.WithCountSum(Users(context),
                    UserCountQueries.GamesMastered(context), UserCountQueries.GamesAssisted(context)),
                2, 5)
            .OrderBy(u => u.Username)
            .ToQueryString();

        OuterWhereClause(sql).Should().NotContain("SELECT",
            "the numeric range selects the page, so a count correlated to the user row inside it " +
            "is paid for by every registered user");
        OuterWhereClause(sql).Should().Contain(">=").And.Contain("<=",
            "both bounds are predicates over the one number, and they share the one join");
    }
}
