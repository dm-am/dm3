using System.Collections.Generic;
using System.Web;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>When an image is put behind a spoiler instead of embedding on sight.</summary>
internal enum ImageGate
{
    /// <summary>Never: the image is shown where it stands.</summary>
    None,

    /// <summary>
    /// Behind a spoiler marked <c>data-bb-gate="image"</c>: the client opens it or
    /// keeps it closed by the reader's own layout, which the server does not know
    /// (the global chat, see BBCODE_RENDERING.md, "Картинки общего чата").
    /// </summary>
    ReaderChoice,

    /// <summary>Always behind a spoiler: the safe rendering, which has no layout to ask.</summary>
    Always
}

/// <summary>
/// <c>[img]address[/img]</c>, with the optional <c>width</c> and <c>height</c>
/// parameters.
/// </summary>
/// <remarks>
/// The markup is shared with the client renderer and the editor node, which is
/// why its attribute set is frozen by BbParserWrapperShould. Sizing flows through
/// CSS custom properties on a wrapper <c>span.bb-image-frame</c>, so the classes
/// of an ancestor can override it through the ordinary cascade; the image itself
/// carries no inline style. A size is a limit, not a stretch: one side given
/// scales the other in proportion, both given fit the picture inside the box.
///
/// There is no <c>alt</c> parameter. A reader never sees the description, and
/// text nobody sees on the page is the place for an insult nobody notices; the
/// grammar reads <c>alt</c> as a parameter the tag does not have, so the tag is
/// printed as typed. The image carries an empty alt of its own, which tells a
/// screen reader to pass it by rather than to read its address aloud.
/// </remarks>
internal sealed class ImageTag : ContentTag
{
    private const string Width = "width";
    private const string Height = "height";

    private static readonly IReadOnlyDictionary<string, ParameterKind> Declared =
        new Dictionary<string, ParameterKind>
        {
            [Width] = ParameterKind.Size,
            [Height] = ParameterKind.Size
        };

    private readonly ImageGate _gate;

    internal ImageTag(ImageGate gate) : base("img", Declared)
    {
        _gate = gate;
    }

    /// <inheritdoc />
    public override string? ReadContent(string raw) => BbAddress.Read(raw);

    /// <inheritdoc />
    /// <remarks>
    /// The content is an address <see cref="BbAddress.Read"/> accepted: one it
    /// refused makes the tag the text that was typed, and this is never asked.
    /// </remarks>
    public override string RenderHtml(TagNode node, string content)
    {
        var encodedUrl = HttpUtility.HtmlAttributeEncode(content);

        // loading/decoding: a page carries up to twenty posts, and images below
        // the fold cost first paint for nothing. Both attributes are inert for
        // anything already on screen.
        var image = $"<img src=\"{encodedUrl}\" class=\"bb-image\" " +
                    $"data-bb-tag=\"img\" loading=\"lazy\" decoding=\"async\" " +
                    "referrerpolicy=\"no-referrer\" alt=\"\" />";

        node.Parameters.TryGetValue(Width, out var width);
        node.Parameters.TryGetValue(Height, out var height);
        if (width != null || height != null)
        {
            var cssVars = new List<string>();
            var dataAttrs = new List<string>();
            if (width != null)
            {
                cssVars.Add($"--bb-image-max-width:{width}px");
                dataAttrs.Add($"data-bb-width=\"{width}\"");
            }

            if (height != null)
            {
                cssVars.Add($"--bb-image-max-height:{height}px");
                dataAttrs.Add($"data-bb-height=\"{height}\"");
            }

            image = $"<span class=\"bb-image-frame\" {string.Join(" ", dataAttrs)} " +
                    $"style=\"{string.Join(";", cssVars)}\">{image}</span>";
        }

        return Gate(image);
    }

    /// <inheritdoc />
    public override string RenderText(TagNode node, string content) => content;

    /// <summary>
    /// The same markup an ordinary [spoiler] emits, because the client wires one
    /// behaviour to a .spoiler-head plus the .spoiler next to it, and a second
    /// shape would need a second implementation of the same toggle.
    /// </summary>
    private string Gate(string image) => _gate switch
    {
        ImageGate.ReaderChoice =>
            $"<a href=\"#\" class=\"spoiler-head\" data-bb-gate=\"image\">{SpoilerTag.DefaultCaption}</a>" +
            $"<div class=\"spoiler\" data-bb-gate=\"image\">{image}</div>",
        ImageGate.Always =>
            $"<a href=\"#\" class=\"spoiler-head\">{SpoilerTag.DefaultCaption}</a>" +
            $"<div class=\"spoiler\">{image}</div>",
        ImageGate.None => image
    };
}
