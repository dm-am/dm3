using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The server side is one constant; the account form and the paging composable of the client are
/// the two copies that cannot share it, read from source.
/// </remarks>
public class PagingContractShould
{
    private static readonly string ClientRoot = Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Web.Client", "src");

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
        ClientCopies.Number("shared/lib/composables/usePaging.ts", "DEFAULT_PAGE_SIZE")
            .Should().Be(PagingPolicy.DefaultPageSize,
                "a different default here shows up as a page that changes size the first time the settings are saved");

        // The same number the server hands a reader who never opened the settings.
        UserSettings.Default.PageSize.Should().Be(PagingPolicy.DefaultPageSize);

        ClientCopies.Number("entities/ticket/api/ticketApi.ts", "DEFAULT_PAGE_SIZE")
            .Should().Be(PagingPolicy.DefaultPageSize, "the list of one's own tickets starts on the same page size");
    }

    [Fact]
    public void AskACursorForNoMoreThanItServes() =>
        ClientCopies.Number("entities/global-chat/model/chatWindow.ts", "LATEST_PAGE_SIZE")
            .Should().BeLessThanOrEqualTo(CursorQuery.MaxLimit,
                "the store reads a short answer as the end of the chat, and a clamped one is short");
}
