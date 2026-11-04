using System.Collections.Generic;
using BBCodeParser;
using BBCodeParser.Tags;
using DM.Infrastructure.Core.Parsing;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// A wrapper whose tag set carries one tag declared the way a tag that puts
/// its value into its markup is declared: with an attribute, secure: false.
/// </summary>
/// <remarks>
/// [private] cannot show it: PrivateBlockMarkup.Normalise rewrites its every spelling first, so a tag
/// nothing else knows about leaves the encoder alone to answer for the value.
/// </remarks>
internal static class BbAttributeTagParser
{
    /// <summary>The wrapper over [note=value] and [code].</summary>
    internal static readonly BbParserWrapper Wrapper = new(new BbParser(
        [
            new Tag("note", "<div class=\"note\"><div class=\"note-head\">{value}</div>", "</div>", true, false),
            new CodeTag("code", "<pre>", "</pre>")
        ],
        BbParser.SecuritySubstitutions, new Dictionary<string, string>()));
}
