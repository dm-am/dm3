using BBCodeParser;
using DM.Domain.Core.Content;

namespace DM.Infrastructure.Core.Parsing;

/// <inheritdoc />
/// <remarks>
/// The text is parsed by the parser of a surface that reads the given tags,
/// and written back from its tree with the names of what the tree reads
/// as markup in lower case (NodeTree.ToBbInLowerCase). No pass over the string
/// decides what a tag is: that is the parser's alone.
///
/// The text written back is the text the parser read, which lacks the
/// characters it removes before reading (see ParserInput). So a text written
/// back is kept only when it differs from what the parser read in nothing but
/// the case of A-Z, and the text as submitted is returned otherwise: when no
/// name changed, as a guard whenever the tree failed to give the text back, and
/// when the page would show the text written back otherwise than the text
/// submitted (a [tip] printed as typed, see below). A text too deep to parse is
/// returned as it came; the save path refuses it on its own
/// (IBbCodeNestingLimit).
/// </remarks>
internal sealed class BbTagNameCase : IBbTagNameCase
{
    private readonly IBbParserProvider _parserProvider;

    /// <inheritdoc cref="BbTagNameCase"/>
    public BbTagNameCase(IBbParserProvider parserProvider) => _parserProvider = parserProvider;

    /// <inheritdoc />
    public string? ToLowerCase(string? source, BbTagSet tags)
    {
        if (string.IsNullOrEmpty(source) || !HasCapital(source)) return source;

        // The game post reads the common tags, [private] included; the comment
        // reads them and [mod]; the description of a game tag reads its own.
        var parser = _parserProvider.GetForSurface(tags switch
        {
            BbTagSet.WithModBlock => BbSurface.Comment,
            BbTagSet.Hint => BbSurface.Hint,
            _ => BbSurface.GamePost
        });

        try
        {
            var tree = parser.Parse(source);
            var written = tree.ToBbInLowerCase();

            var read = ParserInput.Of(source);
            if (written == read || !DiffersInCaseAlone(read, written)) return source;

            // The tree writes the names of every node the grammar accepted, and
            // one such node is still printed as typed: a [tip] that shows
            // nothing, or whose address is refused, which only its parameters
            // tell (TipTag). Lowered, its brackets on the page would change
            // case, so the text is kept only when the page shows it the same.
            return parser.Parse(written).ToHtml() == tree.ToHtml() ? written : source;
        }
        catch (BbParserException)
        {
            return source;
        }
    }

    private static bool HasCapital(string text)
    {
        foreach (var character in text)
        {
            if (character is >= 'A' and <= 'Z') return true;
        }

        return false;
    }

    /// <summary>Whether the two differ in nothing but A-Z written as a-z in the second.</summary>
    private static bool DiffersInCaseAlone(string read, string written)
    {
        if (read.Length != written.Length) return false;

        for (var index = 0; index < read.Length; index++)
        {
            var was = read[index];
            var now = written[index];
            if (was == now) continue;
            if (was is < 'A' or > 'Z' || now != (char)(was + ('a' - 'A'))) return false;
        }

        return true;
    }
}
