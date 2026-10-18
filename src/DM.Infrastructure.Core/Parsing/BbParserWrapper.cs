using BBCodeParser;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// The parser of one rendering, and the entry point that renders through the
/// visibility filter.
/// </summary>
/// <remarks>
/// It used to be much more than that. [img], [link] and [mention] were cut out
/// of the text by regular expressions before the parser ran, a pass rewrote
/// every spelling of [private] into the one the parser knew, another encoded
/// attribute values, and placeholders stood in for what was cut out until the
/// output was put back together. Each of those passes read the markup by a
/// grammar of its own, and every leak of private text this project has had came
/// through a place where two of them disagreed. The parser reads every tag now,
/// by one grammar (see BBCodeParser.Reader), and the text is parsed once: no pass
/// over the string stands before the parse or after it.
/// </remarks>
public partial class BbParserWrapper : IBbParser
{
    private readonly IBbParser _inner;

    /// <summary>
    /// Create the wrapper over the parser that carries the tag set of the rendering.
    /// </summary>
    /// <param name="inner">The parser</param>
    public BbParserWrapper(IBbParser inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public NodeTree Parse(string input) => _inner.Parse(input);

    /// <inheritdoc />
    public Tag[] GetTags() => _inner.GetTags();
}
