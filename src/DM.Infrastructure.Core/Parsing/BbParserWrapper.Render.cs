using System;
using BBCodeParser.Nodes;
using DM.Infrastructure.Core.Parsing.Visitors;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Permission-aware rendering entry points. Parse once, filter by
/// <see cref="RenderContext"/>, emit in the requested audience format.
/// The filter and transform delegates come from
/// <see cref="PermissionFilteringVisitor"/>; they are pure functions of
/// the node and the context.
/// </summary>
public partial class BbParserWrapper
{
    /// <summary>
    /// Render the given BBCode to HTML, applying permission filtering
    /// per <paramref name="ctx"/>. Stripped privacy-sensitive subtrees
    /// produce zero output (no placeholder, no marker). For
    /// <see cref="RenderAudience.AuthorEdit"/> the caller must pass a
    /// wrapper constructed over the author-edit parser variant, whose
    /// tag templates emit <c>data-bb-*</c> round-trip attributes on
    /// <c>[private]</c> / <c>[mod]</c>.
    /// </summary>
    public string RenderHtml(string input, RenderContext ctx)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var tree = Parse(input);
        var plan = PermissionFilteringVisitor.Prepare(input, ctx);

        var html = tree is WrappedNodeTree wrapped
            ? wrapped.ToHtmlFiltered(plan.Filter, plan.Transform)
            : tree.ToHtml(plan.Filter, plan.Transform);

        return html;
    }

    /// <summary>
    /// Render the given BBCode to plain text, applying permission filtering.
    /// <see cref="RenderAudience.PlainText"/> is the canonical use case; other
    /// audiences are accepted but filter the same way.
    /// </summary>
    public string RenderText(string input, RenderContext ctx)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var tree = Parse(input);
        var plan = PermissionFilteringVisitor.Prepare(input, ctx);

        var text = tree is WrappedNodeTree wrapped
            ? wrapped.ToTextFiltered(plan.Filter, plan.Transform)
            : tree.ToText(plan.Filter, plan.Transform);

        return text;
    }

    /// <summary>
    /// Inspect the BBCode input and report whether it contains any
    /// privacy-sensitive tags. Exposed so callers (BbConverter cache
    /// bucket computation) can decide bucketing without parsing twice.
    /// </summary>
    public static bool ContainsPrivacyTags(string input) =>
        !string.IsNullOrEmpty(input) &&
        PermissionFilteringVisitor.Prepare(input, RenderContext.ForPlainText()).HasPrivacyTags;
}
