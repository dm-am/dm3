using BBCodeParser;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;
using DM.Infrastructure.Core.Parsing.Visitors;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// The parser of one rendering, and the entry point that renders through the
/// visibility filter of <see cref="PermissionFilter"/>.
/// </summary>
/// <remarks>
/// No pass over the string stands before the parse or after it: every pass with a
/// grammar of its own is a place where two readings of the markup can disagree.
/// </remarks>
public class BbParserWrapper : IBbParser
{
    private readonly IBbParser _inner;

    public BbParserWrapper(IBbParser inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public NodeTree Parse(string input) => _inner.Parse(input);

    /// <inheritdoc />
    public Tag[] GetTags() => _inner.GetTags();

    /// <summary>
    /// Render to HTML through the filter of <paramref name="ctx"/>.
    /// </summary>
    public string RenderHtml(string input, RenderContext ctx)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var plan = PermissionFilter.Prepare(ctx);
        return Parse(input).ToHtml(plan.Filter, plan.Transform);
    }

    /// <summary>
    /// Render to plain text through the filter of <paramref name="ctx"/>.
    /// </summary>
    public string RenderText(string input, RenderContext ctx)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var plan = PermissionFilter.Prepare(ctx);
        return Parse(input).ToText(plan.Filter, plan.Transform);
    }
}
