using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DM.Domain.Core.Content;

/// <summary>
/// The shape of the [private] block, in one place.
///
/// Two paths depend on spelling it the same way: the renderer, which has to
/// hand the parser a tag it recognises before the visibility filter can see a
/// node at all, and the post save path, which records who each block is for
/// under the key the visitor will later look it up by. A second copy of these
/// rules in the other assembly is how the two halves drift apart — and the last
/// time they did, every private line in the product's own syntax was served to
/// the whole room.
/// </summary>
public static class PrivateBlockMarkup
{
    /// <summary>Tag name of the private addressee block.</summary>
    public const string TagName = "private";

    /// <summary>
    /// Opening tag in any casing, with or without an attribute. The quoted
    /// alternative is tried first so a value keeps its commas and spaces; the
    /// unquoted one runs to the closing bracket. Both quantifiers are bounded
    /// and neither nests, so the pattern stays linear in the content length.
    /// </summary>
    private static readonly Regex OpenTagRegex = new(
        @"\[private(?:=(?<value>""[^""]*""|[^\]]*))?\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Closing tag in any casing.</summary>
    private static readonly Regex CloseTagRegex = new(
        @"\[/private\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Bring every spelling of the tag to the one the parser's tag set
    /// recognises: lower-case name, attribute value in quotes.
    /// </summary>
    /// <remarks>
    /// The underlying parser matches the tag name case-sensitively and its
    /// attribute only when the value is quoted, so of the four spellings a user
    /// can produce only <c>[private="Name"]</c> was ever parsed as a tag. The
    /// other three — including <c>[private=Name]</c>, which is the only form the
    /// editor writes and the only one its help text teaches — were left as plain
    /// text, which means the visitor never saw a node to filter and the private
    /// line was served to every reader of the room.
    ///
    /// Normalising on the render path rather than at the editor is deliberate: a
    /// privacy filter has to hold for whatever reaches it. Any client, any
    /// hand-typed source and anything already stored is covered by one pass, and
    /// nothing downstream has to be trusted to spell the tag a particular way.
    ///
    /// Known cosmetic effect: inside [noparse] the tag is displayed literally, and
    /// there it will now be displayed with quotes the author did not type. Nothing
    /// leaks — noparse content is text by the author's own instruction — and the
    /// alternative is teaching this pass to parse block structure, which is the
    /// job of the parser it runs before.
    /// </remarks>
    public static string Normalise(string input)
    {
        if (string.IsNullOrEmpty(input) ||
            input.IndexOf(TagName, StringComparison.OrdinalIgnoreCase) < 0)
            return input;

        var normalised = OpenTagRegex.Replace(input, match => match.Groups["value"].Success
            ? $"[{TagName}=\"{ReadAttributeValue(match)}\"]"
            : $"[{TagName}]");

        return CloseTagRegex.Replace(normalised, $"[/{TagName}]");
    }

    /// <summary>
    /// Attribute value of every [private=...] block in the source, in document
    /// order, without repeats, spelled exactly the way the parser will report it
    /// to the visibility filter. Blocks without an attribute address nobody and
    /// are skipped.
    /// </summary>
    /// <remarks>
    /// Ordinal distinctness on purpose: the visitor looks the value up in an
    /// ordinal dictionary, so two spellings that differ only in case are two
    /// different blocks here as well.
    ///
    /// Occurrences inside [noparse] are collected too — this scans the source
    /// rather than the tree. That over-collects and never under-collects: an
    /// entry the parser never produces a node for is looked up by nobody.
    /// </remarks>
    public static IReadOnlyList<string> ReadAddresseeAttributes(string? source)
    {
        if (string.IsNullOrEmpty(source) ||
            source.IndexOf(TagName, StringComparison.OrdinalIgnoreCase) < 0)
            return Array.Empty<string>();

        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in OpenTagRegex.Matches(source))
        {
            if (!match.Groups["value"].Success) continue;
            var value = ReadAttributeValue(match);
            if (value.Length > 0 && seen.Add(value)) found.Add(value);
        }

        return found;
    }

    /// <summary>
    /// The attribute value as the parser will report it: a matched pair of
    /// surrounding quotes removed, everything inside them kept verbatim.
    /// </summary>
    /// <remarks>
    /// Any other quote is dropped rather than escaped — an addressee name never
    /// legitimately contains one, and a quote left in place closes the attribute
    /// early and produces a tag the parser rejects again. That matters most for
    /// the one-sided <c>[private="Name]</c>: it used to be passed through
    /// untouched on the theory that a leading quote meant the value was already
    /// quoted, the parser then refused the unterminated attribute, and the block
    /// was served as plain text to every reader of the room — the same leak the
    /// unquoted form had, through the spelling nobody checked.
    /// </remarks>
    private static string ReadAttributeValue(Match match)
    {
        var value = match.Groups["value"].Value;
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value.Replace("\"", string.Empty);
    }
}
