// DM3 addition to the vendored BBCodeParser: this file is new. See Tag.Parameters.

namespace BBCodeParser.Tags
{
    /// <summary>What the value of a named tag parameter may be.</summary>
    public enum ParameterKind
    {
        /// <summary>Any text the value grammar admits, the empty text included.</summary>
        Text,

        /// <summary>
        /// A size in whole pixels: 1 to 9999, digits only, no leading zero.
        /// </summary>
        Size
    }
}
