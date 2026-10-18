using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The server reads every row of the grammar table the way the row says:
/// what each kind of reader is shown, what the plain-text channels and the
/// search keep of a hidden block, and which texts the save path refuses.
/// </summary>
/// <remarks>
/// The table is bbcodeGrammar.cases.ts, and the rule behind it is written down in
/// BBCODE_RENDERING.md, section "Грамматика разметки". Every row is checked on
/// every surface it names, against markup built here from the row's neutral
/// pieces: a tag, text as typed, a hidden block and who reads it, a dash.
///
/// Readers of a hidden block are real readers of a real post: the author, a lead
/// of the game, the owner of the first character the block names (by the
/// addressee snapshot the save path would have built from the same text), and a
/// reader of the room let in by each of the two settings that share addressed
/// text. A reader outside all of them is asked every time, and must see nothing
/// of a hidden block, on the page, in the plain text, and in what the search
/// cuts out of the raw text as its second lock.
///
/// The author-edit rows are checked in BbAuthorEditRoundTripShould, against the
/// pairs the client reads back.
/// </remarks>
public class BbGrammarShould
{
    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeadId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    public static TheoryData<string> DisplayRows() =>
        new(BbGrammarTable.Rows.Where(row => row.Audience == "display").Select(row => row.Id));

    [Fact]
    public void ReadTheWholeTable()
    {
        BbGrammarTable.Rows.Should().HaveCountGreaterThanOrEqualTo(190,
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
            var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(surface);
            var snapshot = SnapshotFor(row);

            foreach (var reader in ReadersOf(row, surface))
            {
                var html = wrapper.RenderHtml(row.Input, reader.Context(surface, snapshot));
                var expected = ExpectedHtml.Of(row.Expect, surface, reader.Kind);
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
            var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(surface);
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

    /// <summary>
    /// A refusal names the spelling it refuses, its place, and the way to fix it:
    /// the right spelling of the block, the closing tag it lacks, or the closing
    /// tag of the [noparse] left open above it.
    /// </summary>
    [Theory]
    [MemberData(nameof(DisplayRows))]
    public void NameThePlaceAndTheFix_WhenABlockIsRefused(string id)
    {
        var row = BbGrammarTable.Row(id);
        if (!row.SaveRefused || !row.Surfaces.Contains(BbSurface.GamePost)) return;

        var refusal = PrivateBlockMarkup.DescribeBalanceRefusal(row.Input);

        refusal.Should().MatchRegex(@"символ \d+");
        refusal.Should().MatchRegex(@"\[private=""Имя""\]|\[/private\]|\[/noparse\]");
    }

    // ════════════════════════════════════════════════════════════════
    // Readers
    // ════════════════════════════════════════════════════════════════

    private sealed record Reader(ReaderKind Kind, Guid ViewerId, bool RoomOverride, bool PostOverride)
    {
        internal RenderContext Context(BbSurface surface, PrivateAddresseeSnapshotView snapshot) => new()
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = new TestSubject
            {
                UserId = ViewerId,
                Role = UserRole.RegularUser,
                IsAuthenticated = true,
                AccessPolicy = AccessPolicy.NotSpecified
            },
            PostAuthorUserId = AuthorId,
            GameLeadUserIds = [LeadId],
            PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
            PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute,
            RoomViewPrivateText = RoomOverride,
            PostSharePrivateWithAll = PostOverride
        };
    }

    /// <summary>Who reads a hidden block, in the table's own words.</summary>
    internal enum ReaderKind
    {
        Stranger,
        Author,
        Leads,
        Addressees,
        Overrides
    }

    /// <summary>
    /// The stranger always; every other kind only where a hidden block is, and
    /// the addressee only where the block names somebody.
    /// </summary>
    private static IEnumerable<Reader> ReadersOf(GrammarRow row, BbSurface surface)
    {
        yield return new Reader(ReaderKind.Stranger, StrangerId, false, false);
        if (!HasHidden(row.Expect)) yield break;

        yield return new Reader(ReaderKind.Author, AuthorId, false, false);
        yield return new Reader(ReaderKind.Leads, LeadId, false, false);
        yield return new Reader(ReaderKind.Overrides, StrangerId, true, false);
        yield return new Reader(ReaderKind.Overrides, StrangerId, false, true);

        if (Addressees(row.Expect).FirstOrDefault() is not null)
        {
            yield return new Reader(ReaderKind.Addressees, OwnerOf(0), false, false);
        }
    }

    /// <summary>
    /// The snapshot the save path builds from the text: every name the hidden
    /// blocks of the row address is a character of the room, owned by a user of
    /// its own, in the order the row names them.
    /// </summary>
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

    /// <summary>The text written inside every hidden block of the row.</summary>
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

    // ════════════════════════════════════════════════════════════════
    // The markup a row stands for
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// The markup the server renders for the pieces of a row, on one surface, for
    /// one kind of reader.
    /// </summary>
    /// <remarks>
    /// Written here from the markup the renderer is known to produce for each
    /// tag, so that a row states its expectation once, neutrally, and the two
    /// sides turn it into their own markup. Text is escaped the way the parser
    /// escapes text (ampersand and angle brackets), a value the way the tag that
    /// writes it encodes it, and a line break is a break on every surface and
    /// never inside [noparse], the one block shown as written. The break typed
    /// straight after a block is in no piece, the block ending its line; a
    /// hidden block is text in the line and ends none.
    ///
    /// A tag that holds hidden blocks its reader does not see and nothing else
    /// but white space is not drawn for that reader at all (see Erased), and a
    /// line that holds nothing but such pieces goes with one of its line breaks
    /// (see WithoutErasedLines). An
    /// item written inside another item, with no list of its own between them,
    /// is drawn as a block rather than an item, so that the browser does not
    /// close the outer item on it.
    /// </remarks>
    internal static class ExpectedHtml
    {
        /// <summary>What the pieces stand in: no list, a list, or an item of one.</summary>
        private enum Around
        {
            Nothing,
            List,
            Item
        }

        internal static string Of(JsonArray pieces, BbSurface surface, ReaderKind reader)
        {
            // A row left with nothing but white space once the reader's erased
            // pieces are gone is drawn as nothing, as a text of white space alone is.
            var all = pieces.Select(piece => piece!.AsObject()).ToList();
            if (all.Any(piece => Erased(piece, reader)) &&
                all.All(piece => Erased(piece, reader) ||
                                 (piece["text"] is { } text && string.IsNullOrWhiteSpace((string)text!))))
            {
                return string.Empty;
            }

            var output = new StringBuilder();
            foreach (var piece in WithoutErasedLines(pieces, reader, atTextEdges: true))
                Piece(output, piece, surface, reader, verbatim: false, Around.Nothing);
            return output.ToString();
        }

        /// <summary>The blocks that end their line: the break typed after one is in no piece.</summary>
        private static readonly HashSet<string> EndsItsLine = ["quote", "spoiler", "nsfw", "mod", "ul", "ol", "li"];

        /// <summary>The leads, as the recipients of every block name them first.</summary>
        private static readonly string[] Leads = ["DungeonMaster", "Assistant"];

        /// <summary>
        /// The pieces as the reader gets them, less every line that held nothing but
        /// pieces erased for that reader, each with one of the line breaks around it.
        /// </summary>
        /// <remarks>
        /// The text of the whole row begins and ends lines; inside a tag only a line
        /// between two breaks is one. The break before a dropped line goes when it is
        /// still there, else the one after, so the lines left keep one break between
        /// each two of them. The break after a block that ends its line is in no piece,
        /// so the block ends a line here by itself, and there is nothing of that break
        /// left to drop: the one after the dropped line goes instead.
        /// </remarks>
        private static List<JsonObject> WithoutErasedLines(JsonArray pieces, ReaderKind reader, bool atTextEdges)
        {
            var objects = pieces.Select(piece => piece!.AsObject()).ToList();

            // Line breaks, as (piece, index); an index of -1 is the end of a line no
            // piece holds a break for, and cannot be dropped.
            var breaks = new List<(int Piece, int At)>();
            var content = new List<bool> { false };
            var erased = new List<bool> { false };

            for (var i = 0; i < objects.Count; i++)
            {
                var piece = objects[i];
                if (piece["text"] is { } text)
                {
                    var value = (string)text!;
                    for (var at = 0; at < value.Length; at++)
                    {
                        // CR LF is one break, as on the page; a CR alone is no break.
                        var crlf = value[at] == '\r' && at + 1 < value.Length && value[at + 1] == '\n';
                        if (value[at] != '\n' && !crlf)
                        {
                            content[^1] = true;
                            continue;
                        }

                        breaks.Add((i, at));
                        content.Add(false);
                        erased.Add(false);
                        if (crlf) at++;
                    }

                    continue;
                }

                if (Erased(piece, reader))
                {
                    erased[^1] = true;
                    continue;
                }

                content[^1] = true;
                if (piece["tag"] is { } tag && EndsItsLine.Contains((string)tag!))
                {
                    breaks.Add((i, -1));
                    content.Add(false);
                    erased.Add(false);
                }
            }

            var dropped = new bool[breaks.Count];
            for (var line = 0; line < content.Count; line++)
            {
                var edge = line == 0 || line == content.Count - 1;
                if (content[line] || !erased[line] || (edge && !atTextEdges)) continue;

                bool Takes(int index) => index >= 0 && index < breaks.Count && !dropped[index] && breaks[index].At >= 0;
                if (Takes(line - 1)) dropped[line - 1] = true;
                else if (Takes(line)) dropped[line] = true;
            }

            var result = new List<JsonObject>();
            for (var i = 0; i < objects.Count; i++)
            {
                var piece = objects[i];
                var gone = breaks.Select((line, index) => (line, index))
                    .Where(entry => entry.line.Piece == i && dropped[entry.index])
                    .Select(entry => entry.line.At)
                    .ToHashSet();
                if (gone.Count == 0)
                {
                    result.Add(piece);
                    continue;
                }

                var value = (string)piece["text"]!;
                var left = new StringBuilder();
                for (var at = 0; at < value.Length; at++)
                {
                    if (!gone.Contains(at))
                    {
                        left.Append(value[at]);
                        continue;
                    }

                    if (value[at] == '\r') at++;
                }

                if (left.Length > 0) result.Add(new JsonObject { ["text"] = left.ToString() });
            }

            return result;
        }

        private static void Piece(StringBuilder output, JsonObject piece, BbSurface surface, ReaderKind reader,
            bool verbatim, Around around)
        {
            if (piece["text"] is { } text)
            {
                var escaped = ((string)text!).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                output.Append(verbatim ? escaped : escaped.Replace("\n", "<br />"));
                return;
            }

            if (piece["dash"] is { } dash)
            {
                output.Append((string)dash! == "short" ? "&ndash;" : "&mdash;");
                return;
            }

            if (piece["hidden"] is JsonObject hidden)
            {
                if (!Sees(hidden, reader)) return;

                // The master and the assistants read every block, and its
                // recipients name them first, as DM2 posts do: as terms, in the
                // plain text of any other name, and not a second time where the
                // author wrote one of them. A block without names names them
                // alone. The recipients ride on the block for the tooltip and
                // are no text of the line.
                var names = hidden["to"]!.AsArray()
                    .Select(name => (string)name!)
                    .Where(name => !Leads.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                output.Append("<span class=\"private-message\" data-recipients=\"")
                    .Append(WebUtility.HtmlEncode(string.Join(", ", Leads.Concat(names))))
                    .Append("\">");
                Children(output, piece, surface, reader, verbatim, around);
                output.Append("</span>");
                return;
            }

            if (Erased(piece, reader)) return;

            var parameters = piece["params"] as JsonObject;
            string? Param(string name) => parameters?[name] is { } value ? (string)value! : null;

            switch ((string)piece["tag"]!)
            {
                case "b":
                    Wrap(output, piece, surface, reader, verbatim, around, "<strong>", "</strong>");
                    break;
                case "i":
                    Wrap(output, piece, surface, reader, verbatim, around, "<em>", "</em>");
                    break;
                case "u":
                    Wrap(output, piece, surface, reader, verbatim, around, "<u>", "</u>");
                    break;
                case "strike":
                    Wrap(output, piece, surface, reader, verbatim, around, "<s>", "</s>");
                    break;
                case "code":
                    Wrap(output, piece, surface, reader, verbatim, around, "<code class=\"code\">", "</code>");
                    break;
                case "noparse":
                    Wrap(output, piece, surface, reader, true, around, "", "");
                    break;
                case "quote":
                    Wrap(output, piece, surface, reader, verbatim, around, "<div class=\"quote\">", "</div>");
                    break;
                case "mod":
                    Wrap(output, piece, surface, reader, verbatim, around, "<div class=\"mod-block\">", "</div>");
                    break;
                case "ul":
                    Wrap(output, piece, surface, reader, verbatim, Around.List, "<ul>", "</ul>");
                    break;
                case "ol":
                    Wrap(output, piece, surface, reader, verbatim, Around.List, "<ol>", "</ol>");
                    break;
                case "li":
                    if (around == Around.Item)
                        Wrap(output, piece, surface, reader, verbatim, Around.Item, "<div class=\"bb-li\">", "</div>");
                    else
                        Wrap(output, piece, surface, reader, verbatim, Around.Item, "<li>", "</li>");
                    break;
                case "tab":
                    output.Append("&nbsp;&nbsp;&nbsp;");
                    break;
                case "spoiler":
                    // A caption stays on the open head as well; the words of a
                    // spoiler without one swap, as on DM2.
                    var caption = Param("text") is { } given ? HttpUtility.HtmlEncode(given) : null;
                    Wrap(output, piece, surface, reader, verbatim, around,
                        $"<a href=\"#\" class=\"spoiler-head\" data-swaptext=\"{caption ?? "Скрыть содержимое"}\">" +
                        (caption ?? "Показать содержимое") +
                        "</a><div class=\"spoiler\">",
                        "</div>");
                    break;
                case "nsfw":
                    Wrap(output, piece, surface, reader, verbatim, around,
                        "<a href=\"#\" class=\"nsfw-head\" data-swaptext=\"Скрыть шокирующий контент\">" +
                        "Показать шокирующий контент</a><div class=\"nsfw-spoiler\">",
                        "</div>");
                    break;
                case "link":
                    output.Append($"<a href=\"{HttpUtility.HtmlAttributeEncode(Param("href"))}\" " +
                                  $"target=\"_blank\" rel=\"noopener\">{HttpUtility.HtmlEncode(Param("text"))}</a>");
                    break;
                case "mention":
                    var login = Param("login")!;
                    output.Append($"<a class=\"bb-mention\" href=\"/users/{Uri.EscapeDataString(login)}\" " +
                                  $"data-bb-tag=\"mention\" data-bb-user=\"{HttpUtility.HtmlAttributeEncode(login)}\">" +
                                  $"@{HttpUtility.HtmlEncode(login)}</a>");
                    break;
                case "img":
                    output.Append(Image(Param, (string?)piece["gate"] == "compact" &&
                                               surface == BbSurface.GlobalChatMessage));
                    break;
                case "tip":
                    // The attributes the tooltip reads, text before the picture
                    // whatever the order they were typed in; the exact string
                    // is held by HintMarkupShould against the client's pairs.
                    output.Append("<span class=\"bb-tip\" tabindex=\"0\"");
                    if (Param("text") is { } tipText)
                        output.Append($" data-tip-text=\"{TipAttribute(tipText)}\"");
                    if (Param("img") is { } tipImage)
                        output.Append($" data-tip-img=\"{TipAttribute(tipImage)}\"");
                    Wrap(output, piece, surface, reader, verbatim, around, ">", "</span>");
                    break;
                default:
                    throw new InvalidOperationException($"The table names a tag nobody drew: {piece["tag"]}");
            }
        }

        private static string TipAttribute(string value) =>
            value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&#39;");

        private static string Image(Func<string, string?> param, bool gated)
        {
            var image = $"<img src=\"{HttpUtility.HtmlAttributeEncode(param("src"))}\" class=\"bb-image\" " +
                        "data-bb-tag=\"img\" loading=\"lazy\" decoding=\"async\" " +
                        "referrerpolicy=\"no-referrer\" alt=\"\" />";

            var width = param("width");
            var height = param("height");
            if (width != null || height != null)
            {
                var styles = new List<string>();
                var data = new List<string>();
                if (width != null)
                {
                    styles.Add($"--bb-image-max-width:{width}px");
                    data.Add($"data-bb-width=\"{width}\"");
                }

                if (height != null)
                {
                    styles.Add($"--bb-image-max-height:{height}px");
                    data.Add($"data-bb-height=\"{height}\"");
                }

                image = $"<span class=\"bb-image-frame\" {string.Join(" ", data)} " +
                        $"style=\"{string.Join(";", styles)}\">{image}</span>";
            }

            return gated
                ? "<a href=\"#\" class=\"spoiler-head\" data-bb-gate=\"image\">Показать содержимое</a>" +
                  $"<div class=\"spoiler\" data-bb-gate=\"image\">{image}</div>"
                : image;
        }

        private static void Wrap(StringBuilder output, JsonObject piece, BbSurface surface, ReaderKind reader,
            bool verbatim, Around around, string open, string close)
        {
            output.Append(open);
            Children(output, piece, surface, reader, verbatim, around);
            output.Append(close);
        }

        private static void Children(StringBuilder output, JsonObject piece, BbSurface surface, ReaderKind reader,
            bool verbatim, Around around)
        {
            if (piece["children"] is not JsonArray children) return;

            // A list draws its items and lifts the text between them, and has no
            // lines of its own; text shown as written is text.
            var shown = verbatim || around == Around.List
                ? children.Select(child => child!.AsObject()).ToList()
                : WithoutErasedLines(children, reader, atTextEdges: false);
            foreach (var child in shown) Piece(output, child, surface, reader, verbatim, around);
        }

        private static bool Sees(JsonObject hidden, ReaderKind reader) =>
            hidden["seenBy"]!.AsArray().Select(kind => (string)kind!).Contains(reader.ToString().ToLowerInvariant());

        /// <summary>
        /// Whether the piece is a tag that holds, for this reader, hidden blocks
        /// they do not see (or tags that hold nothing else) and nothing but white
        /// space besides: such a tag is not drawn for them at all.
        /// </summary>
        private static bool Erased(JsonObject piece, ReaderKind reader)
        {
            if (piece["hidden"] is JsonObject hidden) return !Sees(hidden, reader);
            if (piece["tag"] is null || piece["children"] is not JsonArray { Count: > 0 } children) return false;

            var erased = false;
            foreach (var node in children)
            {
                var child = node!.AsObject();
                if (child["text"] is { } text && string.IsNullOrWhiteSpace((string)text!)) continue;
                if (!Erased(child, reader)) return false;
                erased = true;
            }

            return erased;
        }
    }
}
