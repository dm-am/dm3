#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BBCodeParser;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace DM.Services.Core.Parsing;

/// <summary>
/// Wrapper over IBbParser that handles [img] and [link] tags separately
/// to avoid BBCodeParser corruption issues.
///
/// Strategy: Extract [img] and [link] tags before parsing, replace with placeholders,
/// then restore them after ToHtml()/ToBb().
///
/// Also handles post-processing for tags that need default values (e.g., spoiler).
/// </summary>
public partial class BbParserWrapper : IBbParser
{
    private readonly IBbParser _inner;

    /// <summary>
    /// Default text for spoiler toggle when no title is provided (Russian: "Показать содержимое")
    /// Must match SPOILER_SHOW_TEXT constant in frontend bbcodeConstants.ts
    /// </summary>
    public const string DefaultSpoilerText = "Показать содержимое";

    /// <summary>
    /// Default max width for images (in pixels)
    /// </summary>
    public const int DefaultMaxWidth = 600;

    /// <summary>
    /// Default max height for images (in pixels)
    /// </summary>
    public const int DefaultMaxHeight = 400;

    /// <summary>
    /// Default display text for links without custom text (Russian: "ссылка")
    /// </summary>
    public const string DefaultLinkText = "ссылка";

    /// <summary>
    /// Dangerous URL protocols that can execute JavaScript or embed data.
    /// URLs starting with these protocols will be sanitized to "#".
    /// </summary>
    private static readonly string[] DangerousProtocols = { "javascript:", "data:", "vbscript:" };

    /// <summary>
    /// Blocked hostname patterns for SSRF protection.
    /// Prevents links/images to internal networks.
    /// </summary>
    private static readonly string[] BlockedHostPatterns =
    {
        "localhost",
        "127.",
        "10.",
        "172.16.", "172.17.", "172.18.", "172.19.",
        "172.20.", "172.21.", "172.22.", "172.23.",
        "172.24.", "172.25.", "172.26.", "172.27.",
        "172.28.", "172.29.", "172.30.", "172.31.",
        "192.168.",
        "169.254.",  // Link-local
        "[::1]",     // IPv6 localhost
        "[fe80:",    // IPv6 link-local
        "[fc00:",    // IPv6 unique local
        "[fd00:",    // IPv6 unique local
    };

    /// <summary>
    /// Allowed URL schemes for links and images.
    /// </summary>
    private static readonly string[] AllowedSchemes = { "http://", "https://" };

    /// <summary>
    /// Sanitize URL to prevent XSS attacks and SSRF attacks.
    /// Returns sanitized URL or "#" if dangerous protocol or blocked host detected.
    /// </summary>
    private static string SanitizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "#";

        var trimmed = url.Trim();
        var lower = trimmed.ToLowerInvariant();

        // Block dangerous protocols (javascript:, data:, vbscript:)
        foreach (var protocol in DangerousProtocols)
        {
            if (lower.StartsWith(protocol))
                return "#";
        }

        // Only allow http:// and https:// schemes
        if (!AllowedSchemes.Any(s => lower.StartsWith(s)))
            return "#";

        // Extract hostname for SSRF check
        try
        {
            var uri = new Uri(trimmed);
            var host = uri.Host.ToLowerInvariant();

            // Block internal/private network addresses
            foreach (var pattern in BlockedHostPatterns)
            {
                if (host.StartsWith(pattern) || host == pattern.TrimEnd('.'))
                    return "#";
            }
        }
        catch
        {
            // Invalid URI - block it
            return "#";
        }

        return trimmed;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXTRACTION REGEXES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Match [img=WIDTHxHEIGHT alt="text"]URL[/img] or [img=WIDTH alt="text"]URL[/img]</summary>
    [GeneratedRegex(@"\[img=(\d+)(?:x(\d+))?\s+alt=""([^""]*)""\]([\s\S]*?)\[/img\]", RegexOptions.IgnoreCase)]
    private static partial Regex ImgWithSizeAndAltRegex();

    /// <summary>Match [img=WIDTHxHEIGHT]URL[/img] or [img=WIDTH]URL[/img]</summary>
    [GeneratedRegex(@"\[img=(\d+)(?:x(\d+))?\]([\s\S]*?)\[/img\]", RegexOptions.IgnoreCase)]
    private static partial Regex ImgWithSizeRegex();

    /// <summary>Match [img alt="text"]URL[/img]</summary>
    [GeneratedRegex(@"\[img\s+alt=""([^""]*)""\]([\s\S]*?)\[/img\]", RegexOptions.IgnoreCase)]
    private static partial Regex ImgWithAltRegex();

    /// <summary>Match [img]URL[/img]</summary>
    [GeneratedRegex(@"\[img\]([\s\S]*?)\[/img\]", RegexOptions.IgnoreCase)]
    private static partial Regex ImgRegex();

    /// <summary>Match [link=text]URL[/link]</summary>
    [GeneratedRegex(@"\[link=([^\]]+)\]([\s\S]*?)\[/link\]", RegexOptions.IgnoreCase)]
    private static partial Regex LinkWithTextRegex();

    /// <summary>Match [link]URL[/link]</summary>
    [GeneratedRegex(@"\[link\]([\s\S]*?)\[/link\]", RegexOptions.IgnoreCase)]
    private static partial Regex LinkSimpleRegex();

    /// <summary>Match empty spoiler-head anchor (no title text) for post-processing</summary>
    [GeneratedRegex(@"<a href=""#"" class=""spoiler-head""></a>", RegexOptions.IgnoreCase)]
    private static partial Regex EmptySpoilerHeadRegex();

    /// <summary>
    /// Create wrapper around existing parser
    /// </summary>
    public BbParserWrapper(IBbParser inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public NodeTree Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return _inner.Parse(input);

        // Extract [img] and [link] tags, replace with placeholders
        // Note: [spoiler=X] is NOT supported - only simple [spoiler] handled by BBCodeParser
        var imgList = new List<(string url, int? width, int? height, string? alt)>();
        var linkList = new List<(string? text, string url)>();

        var processed = input;

        // Extract [img=WxH alt="text"]URL[/img] or [img=W alt="text"]URL[/img] (MUST be first)
        processed = ImgWithSizeAndAltRegex().Replace(processed, match =>
        {
            var width = int.TryParse(match.Groups[1].Value, out var w) ? w : (int?)null;
            var height = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var h) ? h : (int?)null;
            var alt = match.Groups[3].Value;
            var url = match.Groups[4].Value;
            var index = imgList.Count;
            imgList.Add((url, width, height, string.IsNullOrEmpty(alt) ? null : alt));
            return $"__IMG_{index}__";
        });

        // Extract [img=WxH]URL[/img] or [img=W]URL[/img] (no alt)
        processed = ImgWithSizeRegex().Replace(processed, match =>
        {
            var width = int.TryParse(match.Groups[1].Value, out var w) ? w : (int?)null;
            var height = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var h) ? h : (int?)null;
            var url = match.Groups[3].Value;
            var index = imgList.Count;
            imgList.Add((url, width, height, null));
            return $"__IMG_{index}__";
        });

        // Extract [img alt="text"]URL[/img] (alt only, no size)
        processed = ImgWithAltRegex().Replace(processed, match =>
        {
            var alt = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            var index = imgList.Count;
            imgList.Add((url, null, null, string.IsNullOrEmpty(alt) ? null : alt));
            return $"__IMG_{index}__";
        });

        // Extract [img]URL[/img] (simple, no size, no alt)
        processed = ImgRegex().Replace(processed, match =>
        {
            var url = match.Groups[1].Value;
            var index = imgList.Count;
            imgList.Add((url, null, null, null)); // null = use default max dimensions
            return $"__IMG_{index}__";
        });

        // Extract [link=text]URL[/link] (MUST be before simple link)
        processed = LinkWithTextRegex().Replace(processed, match =>
        {
            var text = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            var index = linkList.Count;
            linkList.Add((text, url));
            return $"__LINK_{index}__";
        });

        // Extract [link]URL[/link]
        processed = LinkSimpleRegex().Replace(processed, match =>
        {
            var url = match.Groups[1].Value;
            var index = linkList.Count;
            linkList.Add((null, url)); // null text = simple link
            return $"__LINK_{index}__";
        });

        // Parse the rest with BBCodeParser
        var innerTree = _inner.Parse(processed);

        // Return wrapped tree that restores placeholders
        return new WrappedNodeTree(innerTree, imgList, linkList);
    }

    /// <inheritdoc />
    public Tag[] GetTags() => _inner.GetTags();

    /// <summary>
    /// NodeTree wrapper that restores [img] and [link] placeholders in output.
    /// Public so BbConverter can cast to it and call the correct methods.
    ///
    /// Implementation note: Uses int.Parse() for placeholder indices (not TryParse) because:
    /// 1. Placeholders are internally generated via List.Count - always valid integers
    /// 2. Regex pattern (\d+) guarantees only digits are captured
    /// 3. Parse() provides fail-fast behavior if internal contracts are violated
    /// 4. TryParse() would mask bugs in placeholder generation logic
    /// See BBCODE_PIPELINE.md "Why int.Parse() Instead of int.TryParse()" for details.
    /// </summary>
    public class WrappedNodeTree : NodeTree
    {
        private readonly NodeTree _inner;
        private readonly List<(string url, int? width, int? height, string? alt)> _imgList;
        private readonly List<(string? text, string url)> _linkList;

        private static readonly Regex ImgPlaceholder = new(@"__IMG_(\d+)__", RegexOptions.Compiled);
        private static readonly Regex LinkPlaceholder = new(@"__LINK_(\d+)__", RegexOptions.Compiled);

        /// <summary>
        /// Create wrapped node tree
        /// </summary>
        public WrappedNodeTree(NodeTree inner, List<(string url, int? width, int? height, string? alt)> imgList, List<(string? text, string url)> linkList)
            : base(BbParser.SecuritySubstitutions, new Dictionary<string, string>())
        {
            _inner = inner;
            _imgList = imgList;
            _linkList = linkList;
        }

        /// <summary>
        /// Convert to HTML, restoring [img] and [link] as HTML elements
        /// </summary>
        public string ToHtml()
        {
            var html = _inner.ToHtml();

            // Restore images as HTML
            html = ImgPlaceholder.Replace(html, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _imgList.Count)
                {
                    var (url, width, height, alt) = _imgList[index];
                    var safeUrl = SanitizeUrl(url);
                    if (safeUrl == "#") return ""; // Remove dangerous image entirely
                    var encodedUrl = System.Web.HttpUtility.HtmlAttributeEncode(safeUrl);

                    // Build style attribute for dimensions
                    var styleAttr = "";
                    if (width.HasValue || height.HasValue)
                    {
                        // Explicit size specified
                        var styles = new List<string>();
                        if (width.HasValue)
                            styles.Add($"max-width:{width.Value}px");
                        if (height.HasValue)
                            styles.Add($"max-height:{height.Value}px");
                        styleAttr = $" style=\"{string.Join(";", styles)}\"";
                    }
                    else
                    {
                        // Default max dimensions
                        styleAttr = $" style=\"max-width:{DefaultMaxWidth}px;max-height:{DefaultMaxHeight}px\"";
                    }

                    // Build alt attribute (also add data-alt for frontend JS access)
                    var altAttr = "";
                    if (!string.IsNullOrEmpty(alt))
                    {
                        var encodedAlt = System.Web.HttpUtility.HtmlAttributeEncode(alt);
                        altAttr = $" alt=\"{encodedAlt}\" data-alt=\"{encodedAlt}\"";
                    }

                    return $"<img src=\"{encodedUrl}\" class=\"image\" referrerpolicy=\"no-referrer\"{styleAttr}{altAttr} />";
                }
                return match.Value;
            });

            // Restore links as HTML
            html = LinkPlaceholder.Replace(html, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _linkList.Count)
                {
                    var (text, url) = _linkList[index];
                    var safeUrl = SanitizeUrl(url);
                    if (safeUrl == "#") return text ?? ""; // Remove dangerous link, keep text if any
                    var encodedUrl = System.Web.HttpUtility.HtmlAttributeEncode(safeUrl);
                    var displayText = text ?? DefaultLinkText;
                    var encodedText = System.Web.HttpUtility.HtmlEncode(displayText);
                    return $"<a href=\"{encodedUrl}\" target=\"_blank\" rel=\"noopener\">{encodedText}</a>";
                }
                return match.Value;
            });

            // Replace empty spoiler heads with default text
            // This handles [spoiler]content[/spoiler] without title
            html = EmptySpoilerHeadRegex().Replace(html,
                $"<a href=\"#\" class=\"spoiler-head\">{DefaultSpoilerText}</a>");

            return html;
        }

        /// <summary>
        /// Convert to plain text
        /// </summary>
        public string ToText()
        {
            var text = _inner.ToText();

            // Restore images as alt text (if available) or URL
            text = ImgPlaceholder.Replace(text, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _imgList.Count)
                {
                    var (url, _, _, alt) = _imgList[index];
                    return !string.IsNullOrEmpty(alt) ? alt : url;
                }
                return match.Value;
            });

            // Restore links as text or URL
            text = LinkPlaceholder.Replace(text, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _linkList.Count)
                {
                    var (linkText, url) = _linkList[index];
                    return linkText ?? url;
                }
                return match.Value;
            });

            return text;
        }

        /// <summary>
        /// Convert back to BBCode in DM3 format
        /// </summary>
        public string ToBb()
        {
            var bb = _inner.ToBb();

            // Restore images as [img]URL[/img] or [img=WxH]URL[/img] or with alt
            bb = ImgPlaceholder.Replace(bb, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _imgList.Count)
                {
                    var (url, width, height, alt) = _imgList[index];
                    var altPart = !string.IsNullOrEmpty(alt) ? $" alt=\"{alt}\"" : "";

                    if (width.HasValue && height.HasValue)
                        return $"[img={width.Value}x{height.Value}{altPart}]{url}[/img]";
                    if (width.HasValue)
                        return $"[img={width.Value}{altPart}]{url}[/img]";
                    if (!string.IsNullOrEmpty(alt))
                        return $"[img{altPart}]{url}[/img]";
                    return $"[img]{url}[/img]";
                }
                return match.Value;
            });

            // Restore links as [link]URL[/link] or [link=text]URL[/link]
            bb = LinkPlaceholder.Replace(bb, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _linkList.Count)
                {
                    var (text, url) = _linkList[index];
                    return text != null ? $"[link={text}]{url}[/link]" : $"[link]{url}[/link]";
                }
                return match.Value;
            });

            return bb;
        }
    }
}
