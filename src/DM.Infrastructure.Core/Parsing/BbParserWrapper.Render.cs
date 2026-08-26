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
        var plan = PermissionFilteringVisitor.Prepare(ctx);

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
        var plan = PermissionFilteringVisitor.Prepare(ctx);

        var text = tree is WrappedNodeTree wrapped
            ? wrapped.ToTextFiltered(plan.Filter, plan.Transform)
            : tree.ToText(plan.Filter, plan.Transform);

        return text;
    }

    /// <summary>
    /// Render the given BBCode back to BBCode, as the body of a quotation.
    /// </summary>
    /// <remarks>
    /// The context is taken as the caller built it - viewer, author, addressees
    /// and all - and the audience is overridden here, so a caller cannot ask for
    /// a quotation and get a display render by forgetting one field. Two plans
    /// are prepared from it: the quotation's own, which strips [private] and
    /// [quote] unconditionally, and the display plan for the same reader, which
    /// answers the only question the first one cannot - whether what was just
    /// stripped is something this reader would have seen on the page.
    ///
    /// That second answer is what <see cref="QuoteSourceResult.PrivateTextStripped"/>
    /// carries, and it is computed this way rather than as "the text had a
    /// private block" on purpose: a reader who was never shown the block must
    /// not learn that it exists. For them the flag is false, always.
    /// </remarks>
    public QuoteSourceResult RenderQuoteSource(string input, RenderContext ctx)
    {
        if (string.IsNullOrEmpty(input)) return new QuoteSourceResult(string.Empty, false);

        var tree = Parse(input);
        var quotePlan = PermissionFilteringVisitor.Prepare(ctx with { Audience = RenderAudience.QuoteSource });
        var displayPlan = PermissionFilteringVisitor.Prepare(ctx with { Audience = RenderAudience.Display });

        var privateTextStripped = false;

        bool Filter(Node node)
        {
            if (quotePlan.Filter(node)) return true;

            if (node is TagNode tagNode &&
                tagNode.Tag?.Name == PermissionFilteringVisitor.PrivateTagName &&
                displayPlan.Filter(node))
            {
                privateTextStripped = true;
            }

            return false;
        }

        var source = tree is WrappedNodeTree wrapped
            ? wrapped.ToBbFiltered(Filter, quotePlan.Transform)
            : tree.ToBb(Filter, quotePlan.Transform);

        return new QuoteSourceResult(source, privateTextStripped);
    }
}

/// <summary>
/// The body of a quotation and the one thing its author has to be told about it.
/// </summary>
/// <param name="Source">
/// BBCode ready to be put into a composer, without the quotation tag around it.
/// </param>
/// <param name="PrivateTextStripped">
/// Whether a [private] block this reader can see on the page was left out. False
/// for a reader the page does not show one to, so the flag never announces the
/// existence of a block its holder was not meant to know about.
/// </param>
public readonly record struct QuoteSourceResult(string Source, bool PrivateTextStripped);
