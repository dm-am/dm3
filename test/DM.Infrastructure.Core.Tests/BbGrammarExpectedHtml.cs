using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using DM.Infrastructure.Core.Parsing;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The markup the server renders for the pieces of a row, on one surface, for
/// one kind of reader.
/// </summary>
/// <remarks>
/// A row states its expectation once, and each side turns it into its own markup. A tag holding only
/// hidden blocks its reader does not see is not drawn (see Erased), a line of such pieces goes with one
/// break (see WithoutErasedLines), and an item inside an item is drawn as a block.
/// </remarks>
internal static class BbGrammarExpectedHtml
{
    /// <summary>What the pieces stand in: no list, a list, or an item of one.</summary>
    private enum Around
    {
        Nothing,
        List,
        Item
    }

    internal static string Of(JsonArray pieces, BbSurface surface, GrammarReader reader)
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
    /// The break before a dropped line goes, else the one after; a block ending its line holds no break, so
    /// the one after goes.
    /// </remarks>
    private static List<JsonObject> WithoutErasedLines(JsonArray pieces, GrammarReader reader, bool atTextEdges)
    {
        var objects = pieces.Select(piece => piece!.AsObject()).ToList();
        var lines = LinesOf(objects, reader);
        return WithoutBreaks(objects, lines, DroppedBreaks(lines, atTextEdges));
    }

    /// <summary>
    /// The lines of a row: the line breaks, as (piece, index), and for each
    /// line whether it holds content and whether it holds an erased piece.
    /// </summary>
    /// <remarks>
    /// An index of -1 is the end of a line no piece holds a break for, and
    /// cannot be dropped.
    /// </remarks>
    private sealed record Lines(List<(int Piece, int At)> Breaks, List<bool> Content, List<bool> Erased);

    private static Lines LinesOf(List<JsonObject> objects, GrammarReader reader)
    {
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

        return new Lines(breaks, content, erased);
    }

    /// <summary>Which line breaks go with the erased lines.</summary>
    private static bool[] DroppedBreaks(Lines lines, bool atTextEdges)
    {
        var (breaks, content, erased) = lines;
        var dropped = new bool[breaks.Count];
        for (var line = 0; line < content.Count; line++)
        {
            var edge = line == 0 || line == content.Count - 1;
            if (content[line] || !erased[line] || (edge && !atTextEdges)) continue;

            bool Takes(int index) => index >= 0 && index < breaks.Count && !dropped[index] && breaks[index].At >= 0;
            if (Takes(line - 1)) dropped[line - 1] = true;
            else if (Takes(line)) dropped[line] = true;
        }

        return dropped;
    }

    /// <summary>The pieces of a row without the line breaks that went.</summary>
    private static List<JsonObject> WithoutBreaks(List<JsonObject> objects, Lines lines, bool[] dropped)
    {
        var breaks = lines.Breaks;
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

    private static void Piece(StringBuilder output, JsonObject piece, BbSurface surface, GrammarReader reader,
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
            HiddenBlock(output, piece, hidden, surface, reader, verbatim, around);
            return;
        }

        if (Erased(piece, reader)) return;

        Tag(output, piece, surface, reader, verbatim, around);
    }

    private static void HiddenBlock(StringBuilder output, JsonObject piece, JsonObject hidden, BbSurface surface,
        GrammarReader reader, bool verbatim, Around around)
    {
        if (!Sees(hidden, reader)) return;

        // The master and the assistants read every block and its recipients name them first, as DM2 does.
        var names = hidden["to"]!.AsArray()
            .Select(name => (string)name!)
            .Where(name => !Leads.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        output.Append("<span class=\"private-message\" data-recipients=\"")
            .Append(WebUtility.HtmlEncode(string.Join(", ", Leads.Concat(names))))
            .Append("\">");
        Children(output, piece, surface, reader, verbatim, around);
        output.Append("</span>");
    }

    private static void Tag(StringBuilder output, JsonObject piece, BbSurface surface, GrammarReader reader,
        bool verbatim, Around around)
    {
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
                Wrap(output, piece, surface, reader, verbatim, around, SpoilerHead(Param("text")), "</div>");
                break;
            case "nsfw":
                Wrap(output, piece, surface, reader, verbatim, around, NsfwHead, "</div>");
                break;
            case "link":
                output.Append(Link(Param));
                break;
            case "mention":
                output.Append(Mention(Param("login")!));
                break;
            case "img":
                output.Append(Image(Param, (string?)piece["gate"] == "compact" &&
                                           surface == BbSurface.GlobalChatMessage));
                break;
            case "tip":
                Wrap(output, piece, surface, reader, verbatim, around, TipOpen(Param), "</span>");
                break;
            default:
                throw new InvalidOperationException($"The table names a tag nobody drew: {piece["tag"]}");
        }
    }

    /// <summary>The head of a spoiler and the opening of its body.</summary>
    private static string SpoilerHead(string? given)
    {
        // A caption stays on the open head as well; the words of a
        // spoiler without one swap, as on DM2.
        var caption = given is not null ? HttpUtility.HtmlEncode(given) : null;
        return $"<a href=\"#\" class=\"spoiler-head\" data-swaptext=\"{caption ?? "Скрыть содержимое"}\">" +
               (caption ?? "Показать содержимое") +
               "</a><div class=\"spoiler\">";
    }

    /// <summary>The head of an nsfw block and the opening of its body.</summary>
    private const string NsfwHead =
        "<a href=\"#\" class=\"nsfw-head\" data-swaptext=\"Скрыть шокирующий контент\">" +
        "Показать шокирующий контент</a><div class=\"nsfw-spoiler\">";

    private static string Link(Func<string, string?> param) =>
        $"<a href=\"{HttpUtility.HtmlAttributeEncode(param("href"))}\" " +
        $"target=\"_blank\" rel=\"noopener\">{HttpUtility.HtmlEncode(param("text"))}</a>";

    private static string Mention(string login) =>
        $"<a class=\"bb-mention\" href=\"/users/{Uri.EscapeDataString(login)}\" " +
        $"data-bb-tag=\"mention\" data-bb-user=\"{HttpUtility.HtmlAttributeEncode(login)}\">" +
        $"@{HttpUtility.HtmlEncode(login)}</a>";

    /// <summary>
    /// The opening tag of a tooltip: the attributes the tooltip reads, text
    /// before the picture whatever the order they were typed in; the exact
    /// string is held by HintMarkupShould against the client's pairs.
    /// </summary>
    private static string TipOpen(Func<string, string?> param)
    {
        var open = new StringBuilder("<span class=\"bb-tip\" tabindex=\"0\"");
        if (param("text") is { } tipText)
            open.Append($" data-tip-text=\"{TipAttribute(tipText)}\"");
        if (param("img") is { } tipImage)
            open.Append($" data-tip-img=\"{TipAttribute(tipImage)}\"");
        return open.Append('>').ToString();
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
            ? "<a href=\"#\" class=\"spoiler-head\" data-bb-gate=\"image\" data-swaptext=\"Скрыть содержимое\">Показать содержимое</a>" +
              $"<div class=\"spoiler\" data-bb-gate=\"image\">{image}</div>"
            : image;
    }

    private static void Wrap(StringBuilder output, JsonObject piece, BbSurface surface, GrammarReader reader,
        bool verbatim, Around around, string open, string close)
    {
        output.Append(open);
        Children(output, piece, surface, reader, verbatim, around);
        output.Append(close);
    }

    private static void Children(StringBuilder output, JsonObject piece, BbSurface surface, GrammarReader reader,
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

    private static bool Sees(JsonObject hidden, GrammarReader reader) =>
        hidden["seenBy"]!.AsArray().Select(kind => (string)kind!).Contains(reader.ToString().ToLowerInvariant());

    /// <summary>
    /// Whether the piece is a tag that holds, for this reader, hidden blocks
    /// they do not see (or tags that hold nothing else) and nothing but white
    /// space besides: such a tag is not drawn for them at all.
    /// </summary>
    private static bool Erased(JsonObject piece, GrammarReader reader)
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
