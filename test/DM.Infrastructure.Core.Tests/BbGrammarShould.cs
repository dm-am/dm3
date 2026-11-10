using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The server reads every row of the grammar table the way the row says:
/// what each kind of reader is shown, what the plain-text channels and the
/// search keep of a hidden block, and which texts the save path refuses.
/// </summary>
/// <remarks>
/// The table is bbcodeGrammar.cases.ts and the rule BBCODE_RENDERING.md, section "Грамматика разметки".
/// Readers of a hidden block are real readers of a real post, and a stranger is asked every time. The
/// author-edit rows are the client's: the server hands the author the source as typed (BbConverterAuthorEditShould).
/// </remarks>
public class BbGrammarShould
{
    private static readonly Guid LeadId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    public static TheoryData<string> DisplayRows() =>
        new(BbGrammarTable.Rows.Where(row => row.Audience == "display").Select(row => row.Id));

    public static TheoryData<string> RefusedGamePostRows() =>
        new(BbGrammarTable.Rows
            .Where(row => row.Audience == "display" && row.SaveRefused && row.Surfaces.Contains(BbSurface.GamePost))
            .Select(row => row.Id));

    [Fact]
    public void ReadTheWholeTable()
    {
        BbGrammarTable.Rows.Should().NotBeEmpty(
            "the rows are discovered by reading the file, and a marker that stopped matching " +
            "would leave every check below green over nothing");
        BbGrammarTable.Rows.Select(row => row.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(DisplayRows))]
    public void ShowEveryReaderWhatTheRowSays(string id)
    {
        var row = BbGrammarTable.Row(id);
        var failures = new List<string>();

        foreach (var surface in row.Surfaces)
        {
            var wrapper = _parserProvider.GetForSurface(surface);
            var snapshot = SnapshotFor(row);

            foreach (var reader in ReadersOf(row))
            {
                var html = wrapper.RenderHtml(row.Input, reader.Context(surface, snapshot));
                var expected = BbGrammarExpectedHtml.Of(row.Expect, surface, reader.Kind);
                if (html != expected)
                {
                    failures.Add($"[{surface}] {reader.Kind}:\n  input:    {Show(row.Input)}\n" +
                                 $"  actual:   {Show(html)}\n  expected: {Show(expected)}");
                }
            }
        }

        string.Join("\n", failures).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(DisplayRows))]
    public void KeepEveryHiddenBlockOutOfThePlainTextAndTheSearch(string id)
    {
        var row = BbGrammarTable.Row(id);
        var hidden = HiddenTexts(row.Expect).Where(text => text.Length > 0).ToArray();
        var secondLock = new Regex(PrivateBlockMarkup.BlockPattern);

        foreach (var surface in row.Surfaces)
        {
            var wrapper = _parserProvider.GetForSurface(surface);
            var plain = wrapper.RenderText(row.Input, RenderContext.ForPlainText() with { Surface = surface });
            var cut = secondLock.Replace(row.Input, " ");

            foreach (var text in hidden)
            {
                plain.Should().NotContain(text, $"the plain text of {surface} is read by mail and search");
                cut.Should().NotContain(text, "the search cuts hidden blocks out of the text it indexes");
            }
        }
    }

    [Theory]
    [MemberData(nameof(DisplayRows))]
    public void RefuseToSaveExactlyTheRowsTheTableRefuses(string id)
    {
        var row = BbGrammarTable.Row(id);

        foreach (var surface in row.Surfaces)
        {
            var refused = surface == BbSurface.GamePost
                ? !PrivateBlockMarkup.IsBalanced(row.Input)
                : PrivateBlockMarkup.ContainsPrivateMarkup(row.Input);

            refused.Should().Be(row.SaveRefused,
                $"on {surface} the save path has to {(row.SaveRefused ? "refuse" : "accept")} {Show(row.Input)}");
        }
    }

    [Theory]
    [MemberData(nameof(RefusedGamePostRows))]
    public void NameTheSpellingAndTheFixWhenABlockIsRefused(string id)
    {
        var refusal = PrivateBlockMarkup.DescribeBalanceRefusal(BbGrammarTable.Row(id).Input);

        refusal.Should().MatchRegex(@"\[/?[Pp][Rr][Ii][Vv][Aa][Tt][Ee]").And.NotContain("символ");
        refusal.Should().MatchRegex(@"\[private=""Имя""\]|\[/private\]|\[/noparse\]");
    }

    private sealed record Reader(GrammarReader Kind, Guid ViewerId, bool RoomOverride, bool GameOverride)
    {
        internal RenderContext Context(BbSurface surface, PrivateAddresseeSnapshotView snapshot) => new()
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = BbVisibilityRender.Viewer(UserRole.RegularUser, ViewerId),
            PostAuthorUserId = BbVisibilityRender.AuthorId,
            GameLeadUserIds = [LeadId],
            PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
            PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute,
            RoomViewPrivateText = RoomOverride,
            GameShowPrivateMessages = GameOverride
        };
    }

    private static IEnumerable<Reader> ReadersOf(GrammarRow row)
    {
        yield return new Reader(GrammarReader.Stranger, StrangerId, false, false);
        if (!HasHidden(row.Expect)) yield break;

        yield return new Reader(GrammarReader.Author, BbVisibilityRender.AuthorId, false, false);
        yield return new Reader(GrammarReader.Leads, LeadId, false, false);
        yield return new Reader(GrammarReader.Overrides, StrangerId, true, false);
        yield return new Reader(GrammarReader.Overrides, StrangerId, false, true);

        if (Addressees(row.Expect).FirstOrDefault() is not null)
        {
            yield return new Reader(GrammarReader.Addressees, OwnerOf(0), false, false);
        }
    }

    private static PrivateAddresseeSnapshotView SnapshotFor(GrammarRow row)
    {
        var roster = Addressees(row.Expect)
            .Select((name, index) => new PrivateAddressee(CharacterOf(index), name, OwnerOf(index)))
            .ToArray();

        return PrivateAddresseeSnapshot.Read(PrivateAddresseeSnapshot.Build(row.Input, roster));
    }

    private static Guid OwnerOf(int index) => Guid.Parse($"00000000-0000-0000-0001-{index + 1:D12}");

    private static Guid CharacterOf(int index) => Guid.Parse($"00000000-0000-0000-0002-{index + 1:D12}");

    private static IEnumerable<string> Addressees(JsonArray pieces) =>
        Walk(pieces)
            .Where(piece => piece["hidden"] is JsonObject)
            .SelectMany(piece => piece["hidden"]!["to"]!.AsArray().Select(name => (string)name!))
            .Distinct();

    private static bool HasHidden(JsonArray pieces) => Walk(pieces).Any(piece => piece["hidden"] is JsonObject);

    private static IEnumerable<string> HiddenTexts(JsonArray pieces) =>
        Walk(pieces)
            .Where(piece => piece["hidden"] is JsonObject)
            .SelectMany(piece => Walk(piece["children"]!.AsArray()))
            .Where(piece => piece["text"] is not null)
            .Select(piece => ((string)piece["text"]!).Trim());

    private static IEnumerable<JsonObject> Walk(JsonArray pieces)
    {
        foreach (var node in pieces)
        {
            var piece = node!.AsObject();
            yield return piece;
            if (piece["children"] is JsonArray children)
            {
                foreach (var child in Walk(children)) yield return child;
            }
        }
    }

    private static string Show(string value) => value.Replace("\n", "\\n");
}
