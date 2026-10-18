using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace DM.Testing.Markup;

/// <summary>
/// What generated markup is made of, read from the list the client reads too.
/// </summary>
/// <remarks>
/// The list lives in the client tree, markupAlphabet.ts, as strict JSON between
/// two marker comments, and is read here the way BbGrammarTable reads the
/// grammar table: from the file, so a piece added there is a piece both sides
/// are tried on without anybody copying it. What each field holds is written
/// beside the list.
/// </remarks>
public sealed class MarkupAlphabet
{
    private const string Begin = "/* alphabet:begin */";
    private const string End = "/* alphabet:end */";

    private static readonly Lazy<MarkupAlphabet> Loaded = new(Load);

    private MarkupAlphabet(JsonObject root)
    {
        Pairs = ReadPairs(root["pairs"]!);
        Pieces = ReadStrings(root["pieces"]!);
        Names = ReadStrings(root["names"]!);
        Leaves = ReadPairs(root["leaves"]!);
        Addresses = ReadStrings(root["addresses"]!);
        Repeats = root["repeats"]!.AsArray()
            .Select(node => ((string)node![0]!, (int)node[1]!))
            .ToArray();

        var valid = root["valid"]!.AsObject();
        ValidPairs = ReadPairs(valid["pairs"]!);
        ValidLists = ReadPairs(valid["lists"]!);
        ValidItems = ReadPairs(valid["items"]!);
        ValidMod = ReadPairs(valid["mod"]!);
        ValidPrivate = ReadStrings(valid["private"]!);
        ValidPrivateClosings = ReadStrings(valid["privateClosings"]!);
        ValidLeaves = ReadStrings(valid["leaves"]!);
        ValidText = ReadStrings(valid["text"]!);
    }

    /// <summary>The list as the file holds it now.</summary>
    public static MarkupAlphabet Shared => Loaded.Value;

    /// <summary>Where the list is written.</summary>
    public static string File => Path.Combine(RepositoryLayout.Root,
        "src", "DM.Web.Client", "src", "shared", "lib", "testing", "markupAlphabet.ts");

    /// <summary>An opening and a closing tag written around generated content.</summary>
    public IReadOnlyList<(string Open, string Close)> Pairs { get; }

    /// <summary>Everything written one piece at a time.</summary>
    public IReadOnlyList<string> Pieces { get; }

    /// <summary>What the addressee list of a hidden block is made of.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>The tags that hold one address or one login.</summary>
    public IReadOnlyList<(string Open, string Close)> Leaves { get; }

    /// <summary>What is put between the two halves of a leaf, whole.</summary>
    public IReadOnlyList<string> Addresses { get; }

    /// <summary>A piece and how many times it is written in a row.</summary>
    public IReadOnlyList<(string Piece, int Times)> Repeats { get; }

    /// <summary>Tags the grammar accepts whole, on every surface.</summary>
    public IReadOnlyList<(string Open, string Close)> ValidPairs { get; }

    /// <summary>The two lists, written with items only.</summary>
    public IReadOnlyList<(string Open, string Close)> ValidLists { get; }

    /// <summary>An item of a list.</summary>
    public IReadOnlyList<(string Open, string Close)> ValidItems { get; }

    /// <summary>[mod], for the surfaces that have it.</summary>
    public IReadOnlyList<(string Open, string Close)> ValidMod { get; }

    /// <summary>Well spelled openings of a hidden block.</summary>
    public IReadOnlyList<string> ValidPrivate { get; }

    /// <summary>The closing tag of a hidden block, in the cases it is written in.</summary>
    public IReadOnlyList<string> ValidPrivateClosings { get; }

    /// <summary>Images, links, mentions and tabs, each whole.</summary>
    public IReadOnlyList<string> ValidLeaves { get; }

    /// <summary>Text that is no markup and names nothing wrong.</summary>
    public IReadOnlyList<string> ValidText { get; }

    private static MarkupAlphabet Load()
    {
        var text = System.IO.File.ReadAllText(File);
        var start = text.IndexOf(Begin, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (start < 0 || end < start)
            throw new InvalidOperationException($"{File} lost its {Begin} and {End} markers");

        return new MarkupAlphabet(JsonNode.Parse(text[(start + Begin.Length)..end])!.AsObject());
    }

    private static string[] ReadStrings(JsonNode node) =>
        node.AsArray().Select(item => (string)item!).ToArray();

    private static (string, string)[] ReadPairs(JsonNode node) =>
        node.AsArray().Select(item => ((string)item![0]!, (string)item[1]!)).ToArray();
}
