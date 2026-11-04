using System.Diagnostics.CodeAnalysis;

namespace DM.Domain.Core.Content;

/// <summary>
/// What a write path stores of a text its author typed and may edit.
/// </summary>
/// <remarks>
/// The text as it was sent, every space and the case of every tag name included:
/// the editor gets back what was saved (BBCODE_RENDERING.md, "Исходник на
/// правку"), so anything changed on the way in comes back as something the author
/// did not type. The case of a tag name means nothing to the grammar, and no
/// reading of the markup asks it. A text of nothing but whitespace shows nothing,
/// and is stored as the empty text it reads as.
/// </remarks>
public static class AuthoredText
{
    /// <summary>The text as typed; empty for one that holds nothing but whitespace.</summary>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? AsTyped(string? text) =>
        text is not null && string.IsNullOrWhiteSpace(text) ? string.Empty : text;
}
