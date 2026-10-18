using System.Diagnostics.CodeAnalysis;

namespace DM.Domain.Core.Content;

/// <summary>
/// The text an author saves, with the name of every tag in it written in lower
/// case, as the site writes every name.
/// </summary>
/// <remarks>
/// The case of a name means nothing to the grammar: [B] is [b]. The editor, its
/// windows, the help and the author's own edit view all write names in lower
/// case, and a text typed with Caps Lock came back from them looking like
/// somebody else's. So the save path writes the names the way the site does.
/// Only what the grammar reads as markup changes, and only the case of its
/// names: a tag printed as the text it was typed, anything inside a closed
/// [noparse] and every value stay as they were, and the page shows the text as
/// it did.
///
/// An interface for the reason <see cref="IBbCodeNestingLimit"/> is one: what
/// is markup is known to the parser alone, and this assembly cannot reference
/// it. Implemented next to the parser.
/// </remarks>
public interface IBbTagNameCase
{
    /// <summary>
    /// The text with the names of its tags in lower case; the very instance
    /// passed in when nothing is to change.
    /// </summary>
    /// <param name="source">Raw BBCode as the author submitted it</param>
    /// <param name="tags">The tags the text is read with where it is shown</param>
    [return: NotNullIfNotNull(nameof(source))]
    string? ToLowerCase(string? source, BbTagSet tags);
}

/// <summary>The tags a text is read with, by where it is shown.</summary>
/// <remarks>
/// The sets of the surfaces of content differ by [mod] alone, and a name that
/// is no tag where the text is shown is text, which keeps its case. The
/// description of a game tag is read with a set of its own.
/// </remarks>
public enum BbTagSet
{
    /// <summary>
    /// The tags every surface reads: game posts, profile texts, game and
    /// character texts, tickets, private messages.
    /// </summary>
    Common,

    /// <summary>
    /// The common tags and [mod]: comments, topic texts, blogs, reviews, the
    /// global chat.
    /// </summary>
    WithModBlock,

    /// <summary>
    /// The description of a game tag: [tip], [noparse] and [private], and no
    /// tag of a post.
    /// </summary>
    Hint
}
