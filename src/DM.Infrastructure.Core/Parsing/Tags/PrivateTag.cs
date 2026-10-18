using System.Collections.Generic;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;
using DM.Domain.Core.Content;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>
/// <c>[private="..."]...[/private]</c> as a reader gets it: the text, carrying
/// in an attribute who it is for, which the page shows in a tooltip.
/// </summary>
/// <remarks>
/// Who reads the block is not text of the post. It stood after the block as a
/// line, " (Получатели: ...)", and the owner took it out (05.10: "а зачем это
/// вообще текстом, если при наведении показывается?"): the tooltip over the
/// green text says it, and the line stretched the gaps of a justified paragraph
/// around every hidden block. The names ride on the block itself, in
/// <c>data-recipients</c>, for the tooltip to read, and go wherever the block
/// goes: a reader the block is erased for gets neither.
///
/// The game's master and assistants read every hidden block of the game
/// (PermissionFilteringVisitor), so every list names them, first: "DungeonMaster,
/// Assistant, Вася, Петя". They are named the way DM2 posts name them, in Latin
/// letters, as terms, in the same plain text as the names after them. A block
/// that names nobody - written without names, with an empty value or a value of
/// commas alone, or in any spelling the grammar did not accept - names them
/// alone rather than nobody: a list without the leads would read as if nobody
/// saw the block at all.
///
/// The names after them are the value the tag is given, a list separated by
/// commas as the save path reads it (PrivateAddresseeSnapshot): each name
/// without the whitespace around it, an empty one left out, and one the author
/// wrote for a lead left out as well, the leads standing first already. A name
/// is a lead in any case, and only A-Z folds onto a-z, as for a tag name. The
/// list is encoded where the template writes it.
///
/// The block stands in the line, as text does: it ends no line, and the line
/// break typed after it is drawn as typed (it does not carry
/// Tag.DropsLineBreakAfter).
/// </remarks>
internal sealed class PrivateTag : Tag
{
    /// <summary>The leads, as the line names them, in its order.</summary>
    private static readonly string[] Leads = ["DungeonMaster", "Assistant"];

    internal PrivateTag(string openTag, string closeTag)
        : base(PrivateBlockMarkup.TagName, openTag, closeTag, withAttribute: true, sealedByOwnTag: true)
    {
        Hides = true;
    }

    /// <inheritdoc />
    public override string GetOpenHtml(TagNode node, string attributeValue) =>
        base.GetOpenHtml(node, Recipients(attributeValue));

    /// <summary>The recipients of a block given the names it is for.</summary>
    private static string Recipients(string? names)
    {
        var line = new List<string>(Leads);
        if (string.IsNullOrEmpty(names)) return string.Join(", ", line);

        foreach (var part in names.Split(','))
        {
            var name = part.Trim();
            if (name.Length > 0 && !IsLead(name)) line.Add(name);
        }

        return string.Join(", ", line);
    }

    private static bool IsLead(string name)
    {
        foreach (var lead in Leads)
        {
            if (AsciiEqualsIgnoreCase(name, lead)) return true;
        }

        return false;
    }

    private static bool AsciiEqualsIgnoreCase(string left, string right)
    {
        if (left.Length != right.Length) return false;

        for (var index = 0; index < left.Length; index++)
        {
            if (AsciiLower(left[index]) != AsciiLower(right[index])) return false;
        }

        return true;
    }

    private static char AsciiLower(char value) => value is >= 'A' and <= 'Z' ? (char)(value + 32) : value;
}
