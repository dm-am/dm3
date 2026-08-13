using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// One set of page sizes, from the form that offers them to the query that serves
/// them.
/// </summary>
/// <remarks>
/// Four places used to state this apiece and two of them disagreed: the
/// preference DTOs allowed 200, the profile validator demanded less than 200, and
/// the request bound stopped at 100. Choosing 200 in the settings therefore
/// refused the whole form with "некорректное значение" — including whatever else
/// the reader had changed on it — and even a saved 200 could not have been
/// served.
///
/// The server side is one constant now, so what is left to check is the two
/// copies that cannot share it: the account form draws the numbers, and the
/// paging composable caps a request by them. Both live in the client, and both
/// are read from source here — asserting a value against itself would prove
/// nothing.
/// </remarks>
public class PagingContractShould
{
    private static readonly string ClientRoot = Path.Combine(
        SolutionRoot(), "src", "DM.Web.Client", "src");

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DM.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("DM.sln not found above the test binaries");
    }

    [Fact]
    public void OfferExactlyThePageSizesTheApiAccepts()
    {
        var source = File.ReadAllText(Path.Combine(
            ClientRoot, "pages", "account", "sections", "AccountSettingsSection.vue"));

        var match = Regex.Match(source, @"const pagingOptions = \[([^\]]+)\]");
        match.Success.Should().BeTrue("the account form is where a reader picks a page size");

        var offered = match.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();

        offered.Should().Equal(PagingPolicy.AllowedPageSizes,
            "a size the form offers and the endpoint refuses fails the whole settings form");
    }

    [Fact]
    public void CapRequestsAtTheLargestSizeAReaderMaySave()
    {
        var source = File.ReadAllText(Path.Combine(
            ClientRoot, "shared", "lib", "composables", "usePaging.ts"));

        var match = Regex.Match(source, @"export const MAX_API_PAGE_SIZE = (\d+)");
        match.Success.Should().BeTrue("every list request is capped through this constant");

        int.Parse(match.Groups[1].Value).Should().Be(PagingPolicy.MaxPageSize,
            "capped lower, a saved preference silently does nothing; higher, the request is refused");
    }

    [Fact]
    public void StartEveryReaderOnTheSamePageSize()
    {
        var source = File.ReadAllText(Path.Combine(
            ClientRoot, "shared", "lib", "composables", "usePaging.ts"));

        var sizes = Regex.Matches(source, @"(?<!polls)PerPage: (\d+)")
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToArray();

        sizes.Should().NotBeEmpty("the composable states the fallback for a reader with no preference");
        sizes.Should().AllBeEquivalentTo(PagingPolicy.DefaultPageSize,
            "a different default here shows up as a page that changes size the first time the settings are saved");

        // The same number the server hands a reader who never opened the settings.
        var settings = UserSettings.Default.Paging;
        settings.PostsPerPage.Should().Be(PagingPolicy.DefaultPageSize);
        settings.CommentsPerPage.Should().Be(PagingPolicy.DefaultPageSize);
        settings.TopicsPerPage.Should().Be(PagingPolicy.DefaultPageSize);
        settings.MessagesPerPage.Should().Be(PagingPolicy.DefaultPageSize);
        settings.EntitiesPerPage.Should().Be(PagingPolicy.DefaultPageSize);
    }

    [Fact]
    public void AcceptEveryOfferedSizeAsARequestBound()
    {
        // The bound on PagingQuery.Take is a Range attribute, so it is asserted
        // through the value it guards rather than by reading the attribute back.
        PagingPolicy.AllowedPageSizes.Max().Should().Be(PagingPolicy.MaxPageSize);
        PagingPolicy.AllowedPageSizes.Should().Contain(PagingPolicy.DefaultPageSize);
        PagingPolicy.AllowedPageSizes.Should().BeInAscendingOrder();
    }
}
