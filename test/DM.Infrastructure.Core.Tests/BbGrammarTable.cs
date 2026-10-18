using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Testing.Markup;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The rows of the grammar table the client and the server are both held to.
/// </summary>
/// <remarks>
/// The table lives in the client tree, bbcodeGrammar.cases.ts, because that is
/// where the other half of it runs. Its rows are strict JSON between two marker
/// comments, one row per line, and this reads them the way
/// BbAuthorEditRoundTripShould reads the author-edit pairs: from the file, so
/// that a row added there is a row checked here without anybody copying it.
/// </remarks>
internal static class BbGrammarTable
{
    private const string Begin = "/* grammar:begin */";
    private const string End = "/* grammar:end */";

    internal static string File => Path.Combine(RepositoryLayout.Root,
        "src", "DM.Web.Client", "src", "shared", "lib", "utils", "bbcodeGrammar.cases.ts");

    private static readonly Lazy<IReadOnlyList<GrammarRow>> Loaded = new(Load);

    internal static IReadOnlyList<GrammarRow> Rows => Loaded.Value;

    internal static GrammarRow Row(string id) => Rows.Single(row => row.Id == id);

    private static IReadOnlyList<GrammarRow> Load()
    {
        var text = System.IO.File.ReadAllText(File);
        var start = text.IndexOf(Begin, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (start < 0 || end < start)
            throw new InvalidOperationException($"{File} lost its {Begin} and {End} markers");

        var rows = JsonNode.Parse(text[(start + Begin.Length)..end])!.AsArray();
        return rows.Select(node => new GrammarRow(node!.AsObject())).ToArray();
    }
}

/// <summary>One row of the grammar table.</summary>
internal sealed class GrammarRow
{
    internal GrammarRow(JsonObject row)
    {
        Id = (string)row["id"]!;
        Rule = (string)row["rule"]!;
        Input = (string)row["input"]!;
        Surface = (string)row["surface"]!;
        Audience = (string?)row["audience"] ?? "display";
        Broken = (string?)row["broken"];
        SaveRefused = (string?)row["save"] == "refused";
        Expect = row["expect"]!.AsArray();
    }

    internal string Id { get; }
    internal string Rule { get; }
    internal string Input { get; }
    internal string Surface { get; }
    internal string Audience { get; }
    internal string? Broken { get; }
    internal bool SaveRefused { get; }
    internal JsonArray Expect { get; }

    /// <summary>
    /// The surfaces the row speaks for: all five surfaces of content for "any".
    /// The hint has a tag set of its own, and every row for it names it.
    /// </summary>
    internal IEnumerable<BbSurface> Surfaces => Surface == "any"
        ? MarkupSurfaces.Content
        : [Enum.Parse<BbSurface>(Surface)];

    /// <summary>The BBCode the author's editor is given back, for an author_edit row.</summary>
    internal string Source => (string)Expect.Single()!["source"]!;

    public override string ToString() => Id;
}
