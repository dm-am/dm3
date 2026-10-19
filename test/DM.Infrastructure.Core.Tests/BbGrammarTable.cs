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
/// <remarks>Read from bbcodeGrammar.cases.ts in the client, strict JSON between two marker comments, one row per line.</remarks>
internal static class BbGrammarTable
{

    internal static string File => Path.Combine(RepositoryLayout.Root,
        "src", "DM.Web.Client", "src", "shared", "lib", "utils", "bbcodeGrammar.cases.ts");

    private static readonly Lazy<IReadOnlyList<GrammarRow>> Loaded = new(Load);

    internal static IReadOnlyList<GrammarRow> Rows => Loaded.Value;

    internal static GrammarRow Row(string id) => Rows.Single(row => row.Id == id);

    private static IReadOnlyList<GrammarRow> Load() => ClientTable.Read(File, "grammar").AsArray()
        .Select(node => new GrammarRow(node!.AsObject()))
        .ToArray();
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

    public override string ToString() => Id;
}

/// <summary>Who reads private text, in the table's own words.</summary>
internal enum GrammarReader
{
    Stranger,
    Author,
    Leads,
    Addressees,
    Overrides
}
