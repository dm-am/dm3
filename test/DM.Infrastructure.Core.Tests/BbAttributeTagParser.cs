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
/// The product tag of that shape is [private], and it cannot show what the
/// attribute tests are about. PrivateBlockMarkup.Normalise rewrites its every
/// spelling to the quoted one before the extractions run, and strips the
/// attribute whose value carries a bracket, so a broken encoder passes with
/// it. A tag nothing else in the pipeline knows about leaves the encoder alone
/// to answer for the value.
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
