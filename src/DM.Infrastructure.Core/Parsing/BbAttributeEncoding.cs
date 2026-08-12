using System.Web;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// The HTML encoding a tag attribute value gets on the way into the parser, and
/// its inverse.
/// </summary>
/// <remarks>
/// Two ends depend on the pair being exact, and they live in different files.
/// <see cref="BbParserWrapper"/> encodes, because the parser substitutes the
/// value into the markup it emits as it stands, and the value is author text.
/// <see cref="Visitors.PermissionFilteringVisitor"/> decodes, because the
/// [private] addressee snapshot is keyed by the raw text of the post, resolved
/// once at save time (see PrivateAddresseeSnapshot). Written apart, the two
/// drifted the moment a character name carried anything HTML gives meaning to:
/// HtmlEncode covers the apostrophe as well, so "D'Artagnan" reached the lookup
/// as "D&amp;#39;Artagnan", matched no entry, and the block was hidden from the
/// player it addressed. The failure direction was closed rather than open, which
/// is why it stayed silent.
///
/// Decode is the exact inverse of Encode for every input, because Encode
/// replaces the ampersand first: entity text the author typed himself comes back
/// as the same entity text.
/// </remarks>
internal static class BbAttributeEncoding
{
    /// <summary>Encode one attribute value for substitution into markup.</summary>
    internal static string Encode(string value) => HttpUtility.HtmlEncode(value);

    /// <summary>
    /// The value as the author wrote it, from what the parser reports.
    /// </summary>
    internal static string Decode(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : HttpUtility.HtmlDecode(value);
}
