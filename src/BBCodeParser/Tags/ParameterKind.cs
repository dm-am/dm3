// DM3 addition to the vendored BBCodeParser: this file is new. See Tag.Parameters.

namespace BBCodeParser.Tags
{
    /// <summary>What the value of a named tag parameter may be.</summary>
    public enum ParameterKind
    {
        /// <summary>Any text the value grammar admits, the empty text included.</summary>
        Text,

        /// <summary>
        /// A size in whole pixels: 1 to <see cref="ParameterLimits.MaxPixels" />,
        /// digits only, no leading zero.
        /// </summary>
        Size
    }

    /// <summary>The bounds of the parameter values.</summary>
    public static class ParameterLimits
    {
        /// <summary>
        /// The largest size in pixels an image takes. A size is a bound, and no
        /// screen is this wide, so a larger one would bound nothing. The client
        /// holds a copy (MAX_PIXELS in bbcodeTagSet.ts), held to this one by
        /// ClientFieldLimitsShould.
        /// </summary>
        public const int MaxPixels = 9999;
    }
}
