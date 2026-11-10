using System.Collections.Generic;
using System.Web;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing.Tags;

/// <summary>When an image is put behind a spoiler instead of embedding on sight.</summary>
internal enum ImageGate
{
    None,

    /// <summary>
    /// Behind a spoiler marked <c>data-bb-gate="image"</c>, which the client opens by
    /// the reader's layout (BBCODE_RENDERING/grammar.md, "Картинки глобального чата").
    /// </summary>
    ReaderChoice,

    /// <summary>Always behind a spoiler: the safe rendering, which has no layout to ask.</summary>
    Always
}

/// <remarks>
/// The markup is shared with the client and the editor (BbImageTagShould). A size
/// is a limit set through CSS properties on the frame, so an ancestor can override
/// it. No <c>alt</c> parameter: text nobody sees is the place for an insult nobody
/// notices; the empty alt keeps a screen reader from reading the address.
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

        // Lazy: images below the fold of a page of posts cost first paint for nothing.
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
    /// The markup of a [spoiler] without a caption, so that the client toggles and
    /// captions both the same way.
    /// </summary>
    private string Gate(string image) => _gate switch
    {
        ImageGate.ReaderChoice => $"{SpoilerTag.Open(null, " data-bb-gate=\"image\"")}{image}</div>",
        ImageGate.Always => $"{SpoilerTag.Open(null)}{image}</div>",
        ImageGate.None => image
    };
}
