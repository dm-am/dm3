using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using BBCodeParser;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;
using DM.Domain.Core.Content;

namespace DM.Infrastructure.Core.Parsing;

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
    private readonly Regex? _attributeTagPattern;
    private readonly bool _spoilerGatedImages;

    /// <summary>
    /// Default text for spoiler toggle when no title is provided (Russian: "Показать содержимое")
    /// Must match SPOILER_SHOW_TEXT constant in frontend bbcodeConstants.ts
    /// </summary>
    public const string DefaultSpoilerText = "Показать содержимое";

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
    /// Host names that name the reader's own machine by definition — RFC 6761
    /// reserves them for exactly that, so no lookup is needed to know it.
    /// </summary>
    private static readonly string[] BlockedHostNames = { "localhost" };

    /// <summary>
    /// Allowed URL schemes for links and images.
    /// </summary>
    private static readonly string[] AllowedSchemes = { "http://", "https://" };

    /// <summary>
    /// Sanitize URL to prevent XSS and requests into the reader's own network.
    /// Returns sanitized URL or "#" if dangerous protocol or blocked host detected.
    /// </summary>
    private static string SanitizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "#";

        var trimmed = url.Trim();
        var lower = trimmed.ToLowerInvariant();

        // A URI carries no raw whitespace and no control characters (RFC 3986),
        // and one that does is not merely malformed — it is the lever that turns
        // one quoted attribute into two. The <img> built from this URL can end
        // up nested inside another element's attribute value, and there the
        // browser closes that attribute at the quote opening src=", then reads
        // what follows as attributes of the outer element: a space inside the
        // URL becomes an attribute separator and "onmouseover=alert(1)" an event
        // handler on someone else's markup. Trim above still forgives the
        // newlines an editor leaves around a pasted address.
        if (trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            return "#";

        // Block dangerous protocols (javascript:, data:, vbscript:)
        foreach (var protocol in DangerousProtocols)
        {
            if (lower.StartsWith(protocol))
                return "#";
        }

        // Only allow http:// and https:// schemes
        if (!AllowedSchemes.Any(s => lower.StartsWith(s)))
            return "#";

        try
        {
            if (IsBlockedHost(new Uri(trimmed).Host.ToLowerInvariant()))
                return "#";
        }
        catch
        {
            // Invalid URI - block it
            return "#";
        }

        return trimmed;
    }

    /// <summary>
    /// Whether the host names the reader's own machine or private network.
    /// </summary>
    /// <remarks>
    /// Naming the risk right matters here, because it was recorded wrong: the
    /// server never requests these addresses — the reader's browser does, when it
    /// renders the src/href — so this is not SSRF protection. What it prevents is
    /// someone else's post reaching services the reader happens to run on loopback
    /// or on their LAN. A server-side fetch of a user-supplied URL is a separate
    /// threat and needs its own check, made after DNS resolution against the
    /// address the connection actually goes to.
    ///
    /// The address has to be compared parsed rather than as a string prefix.
    /// `new Uri` folds 127.1, 2130706433 and 0177.0.0.1 into the same four bytes,
    /// so those spellings used to be covered by accident, but it leaves an
    /// IPv4-mapped literal such as [::ffff:127.0.0.1] exactly as written, and no
    /// list of prefixes covers the IPv6 ranges — fd00: is one address block out of
    /// the whole of fc00::/7. A host NAME that resolves to a private address
    /// (wildcard-DNS services point any name at 127.0.0.1) still passes: only a
    /// lookup would catch it, and this runs on the render path.
    /// </remarks>
    private static bool IsBlockedHost(string host)
    {
        // Uri.Host keeps the brackets around an IPv6 literal; IPAddress rejects them.
        var literal = host.Length > 1 && host[0] == '[' && host[^1] == ']'
            ? host[1..^1]
            : host;

        if (!IPAddress.TryParse(literal, out var address))
            return BlockedHostNames.Any(name =>
                host == name || host.EndsWith($".{name}", StringComparison.Ordinal));

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6UniqueLocal
                || address.Equals(IPAddress.IPv6Any);

        var octets = address.GetAddressBytes();
        return octets[0] switch
        {
            0 => true,                                  // "this network" — resolves to loopback on some stacks
            10 => true,                                 // RFC 1918
            100 => octets[1] >= 64 && octets[1] <= 127, // RFC 6598 shared address space
            169 => octets[1] == 254,                    // link-local, where cloud metadata lives
            172 => octets[1] >= 16 && octets[1] <= 31,  // RFC 1918
            192 => octets[1] == 168,                    // RFC 1918
            _ => false,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PLACEHOLDERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Character bracketing the placeholder that stands in for an extracted
    /// [img] / [link] / [mention] while the rest of the text goes through the
    /// parser.
    /// </summary>
    /// <remarks>
    /// The whole point of it is that the author's own text can never be mistaken
    /// for one. The previous spelling — __IMG_0__ — was ordinary text, so
    /// restoration could not tell the placeholder it had written itself from the
    /// same characters typed by the author: a post that contained __IMG_0__
    /// alongside any [img] got a second copy of that image pasted at that spot,
    /// including into places where plain text can never become an element — the
    /// author line of a [quote="..."], the addressee attribute of a
    /// [private="..."]. What holds the guarantee is
    /// <see cref="StripPlaceholderMarkers"/>, which removes the character from
    /// the input before extraction: past that line, every occurrence in the
    /// string is one this class put there, whatever the character is.
    ///
    /// It used to be U+0001, and cannot be any longer. BbParser.Parse now
    /// normalises its input and strips the control characters out of it, and the
    /// string handed to it is this one, placeholders and all — a sentinel that
    /// has to survive the parse cannot be a character the parse removes. U+E000
    /// is in the private use area: it has no meaning of its own anywhere, which
    /// is what makes it usable as one here.
    /// </remarks>
    private const char PlaceholderMarker = '\uE000';

    /// <summary>Placeholder kind — one letter per extracted tag.</summary>
    private const char ImageKind = 'I';

    /// <inheritdoc cref="ImageKind"/>
    private const char LinkKind = 'L';

    /// <inheritdoc cref="ImageKind"/>
    private const char MentionKind = 'M';

    /// <inheritdoc cref="ImageKind"/>
    private const char VerbatimKind = 'V';

    /// <summary>Build the placeholder standing in for an extracted tag.</summary>
    private static string Placeholder(char kind, int index) =>
        $"{PlaceholderMarker}{kind}{index}{PlaceholderMarker}";

    /// <summary>
    /// Match one kind of placeholder, capturing its index. Built from the same
    /// two constants the placeholder itself is built from, so the writing side
    /// and the reading side cannot drift apart.
    /// </summary>
    private static Regex PlaceholderPattern(char kind) =>
        new($"{PlaceholderMarker}{kind}(\\d+){PlaceholderMarker}", RegexOptions.Compiled);

    /// <summary>
    /// Drop the marker from the input before extraction. See
    /// <see cref="PlaceholderMarker"/> for why this is the whole guarantee.
    /// </summary>
    private static string StripPlaceholderMarkers(string input) =>
        input.IndexOf(PlaceholderMarker) < 0
            ? input
            : input.Replace(PlaceholderMarker.ToString(), string.Empty);

    // ═══════════════════════════════════════════════════════════════════════
    // EXTRACTION REGEXES
    // ═══════════════════════════════════════════════════════════════════════

    // ───────────────────────────────────────────────────────────────────────
    // Every pattern below that scans for a closing tag with a lazy [\s\S]*? is
    // built on the NonBacktracking engine, and the ones that read an attribute
    // value stop at an opening bracket.
    //
    // The reason is cost. A lazy scan with a literal terminator is quadratic on
    // text that opens a tag and never closes it: each of those openings walks
    // the whole remainder before failing, so the work grows with the square of
    // the post. Measured on a post an author can type by hand —
    // [img][code][link=x][quote=" repeated — 17 KB cost 3.6 s of CPU and 34 KB
    // cost 16.5 s, on every render, for every reader, because the render is not
    // cached by design (see PERFORMANCE.md). NonBacktracking simulates all
    // start positions in one pass, so the same text costs one scan; it matches
    // exactly what the backtracking engine matched, so no content changes.
    //
    // The content classes are not narrowed to "up to the next bracket": a URL may
    // legitimately carry brackets, [img]http://[::1]/x.png[/img] being the
    // ordinary case. They are narrowed to what a URL is — see UrlContent, and see
    // what a lazy scan over [\s\S] cost before it was. Attribute values are the
    // opposite — the parser itself refuses a bracket inside one, because a bracket
    // there means the attribute was never closed — so those classes exclude it and
    // stay linear on the backtracking engine, which the lookahead in
    // ImgAttributeRegex requires.
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// What may stand between the opening and the closing tag of an [img] or a
    /// [link]: one URL, and nothing else.
    /// </summary>
    /// <remarks>
    /// Two rules, both read off what a URL is rather than off what an attack
    /// looks like. No whitespace, because RFC 3986 gives a URI none — SanitizeUrl
    /// already refuses one that carries any. And no closing BBCode tag, because
    /// "[/" is not a sequence an address contains.
    ///
    /// Written [\s\S]*? the content was whatever stood before the next [/img],
    /// so one opening tag the author forgot to close reached across every tag
    /// between the two and swallowed them all. What it swallowed became the URL:
    /// a whole [private] block ended up inside a value that never becomes a
    /// node, so the visibility filter had nothing to hide and the text walk
    /// handed the block on verbatim — into the stored search text, and from
    /// there into the preview shown to every reader of the room. The same
    /// capture then failed SanitizeUrl on the whitespace it carried and the
    /// display path dropped the image whole, so the author's own paragraph
    /// disappeared from the page while the index kept it.
    ///
    /// A bracket that opens no tag is still an ordinary URL character and stays
    /// allowed: http://[::1]/x.png is how a literal IPv6 host is spelled, and
    /// ?filter[name]=x is an ordinary query string. Whitespace AROUND the address
    /// stays allowed too — an editor leaves newlines around a pasted URL and
    /// SanitizeUrl trims them — so the padding sits outside this class rather than
    /// in it, and one token is all that may stand between the two.
    ///
    /// What no longer matches is an opening tag reaching past a closing tag that
    /// is not its own. Such a tag is left where it stands and rendered as the text
    /// it is, which costs nobody a word — the alternative, refusing the whole
    /// post, guesses at what the author meant by a tag they did not close and
    /// takes the rest of the post down with the guess.
    /// </remarks>
    private const string UrlContent = @"(?:[^\s\[]|\[[^\s/])*?";

    /// <summary>Match [img=WIDTHxHEIGHT alt="text"]URL[/img] or [img=WIDTH alt="text"]URL[/img]</summary>
    private static readonly Regex ImgWithSizeAndAlt = new(
        $@"\[img=(\d+)(?:x(\d+))?\s+alt=""([^""]*)""\]\s*({UrlContent})\s*\[/img\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>Match [img=WIDTHxHEIGHT]URL[/img] or [img=WIDTH]URL[/img]</summary>
    private static readonly Regex ImgWithSize = new(
        $@"\[img=(\d+)(?:x(\d+))?\]\s*({UrlContent})\s*\[/img\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>Match [img alt="text"]URL[/img]</summary>
    private static readonly Regex ImgWithAlt = new(
        $@"\[img\s+alt=""([^""]*)""\]\s*({UrlContent})\s*\[/img\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>Match [img]URL[/img]</summary>
    private static readonly Regex Img = new(
        $@"\[img\]\s*({UrlContent})\s*\[/img\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>Match [img="URL"] and [img=URL], the standalone attribute form</summary>
    /// <remarks>
    /// The URL is the attribute here, not the content, so none of the four
    /// patterns above saw it and the tag went to the inner parser, which
    /// substitutes the value into its template as written. That is the whole of
    /// the URL handling this wrapper does — scheme check, loopback and private
    /// address refusal, the spoiler gate on public surfaces, referrerpolicy and
    /// lazy loading — skipped for a spelling a person can type.
    ///
    /// Applied after every content form, and the digits-only case is excluded,
    /// so [img=200]…[/img] stays a size rather than becoming an address.
    /// </remarks>
    [GeneratedRegex(@"\[img=(?!\d+(?:x\d+)?[\]\s])""?([^""\[\]]+)""?\]", RegexOptions.IgnoreCase)]
    private static partial Regex ImgAttributeRegex();

    /// <summary>Match [link=text]URL[/link]</summary>
    private static readonly Regex LinkWithText = new(
        $@"\[link=([^\]]+)\]\s*({UrlContent})\s*\[/link\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>Match [link]URL[/link]</summary>
    private static readonly Regex LinkSimple = new(
        $@"\[link\]\s*({UrlContent})\s*\[/link\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>Match [mention="username"] - standalone tag, no closing tag</summary>
    private static readonly Regex Mention = new(
        @"\[mention=""([^""]+)""\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>Match a [code] or [noparse] block, whose content is shown as written</summary>
    /// <remarks>
    /// The only extraction here whose content is markup on purpose, so it is
    /// the only one that cannot stop at an opening bracket. Linear time comes
    /// from the engine instead: NonBacktracking simulates every start position
    /// in one pass, so an input made of unclosed [code] costs one scan rather
    /// than one scan per opening. That engine supports no backreference, which
    /// is why the two tag names are spelled out instead of matched with \1, and
    /// no Compiled, which is why this is a plain field rather than a generated
    /// regex.
    /// </remarks>
    private static readonly Regex VerbatimBlock = new(
        @"\[code\][\s\S]*?\[/code\]|\[noparse\][\s\S]*?\[/noparse\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>Match the placeholder that stands in for one extracted verbatim block.</summary>
    private static readonly Regex VerbatimPlaceholder = PlaceholderPattern(VerbatimKind);

    /// <summary>Either half of the tag whose audience the parse decides.</summary>
    private static readonly Regex PrivilegedMarkup = new(
        $@"\[/?{PrivateBlockMarkup.TagName}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Whether a span may be cut out of the text before the parser reads it.
    /// </summary>
    /// <remarks>
    /// One sentence holds the whole of the privacy guarantee on this path:
    /// nothing removed before the parse may carry a block whose audience the
    /// parse decides. An extraction files its content in a side list and leaves
    /// a placeholder behind, so that content never becomes a node, is never
    /// offered to the visibility filter, and comes back verbatim in whatever
    /// the tree walk emits — which is exactly what the stored search text is
    /// built from, and what the plain-text audience of the API returns.
    ///
    /// Refusing to extract leaves the tag where it stands. [img] and [link] are
    /// not in the parser's tag set, so they render as the text they are, and the
    /// [private] block inside them becomes the node it should have been all
    /// along — hidden from whoever the filter hides it from, on every surface.
    ///
    /// <see cref="UrlContent"/> already makes a well-formed block impossible to
    /// swallow. This covers the rest of the surface: the alt attribute, which is
    /// not a URL and admits both halves of the tag, and the spellings a block
    /// takes when the author never closed it — refused at the save path by
    /// PrivateBlockMarkup.IsBalanced, which a render path may not depend on.
    /// </remarks>
    private static bool MayExtract(Match match) => !PrivilegedMarkup.IsMatch(match.Value);

    /// <summary>Match empty spoiler-head anchor (no title text) for post-processing</summary>
    [GeneratedRegex(@"<a href=""#"" class=""spoiler-head""></a>", RegexOptions.IgnoreCase)]
    private static partial Regex EmptySpoilerHeadRegex();

    /// <summary>
    /// Match the opening tag of every tag whose attribute value the parser
    /// substitutes into the markup, capturing the name and the value.
    /// </summary>
    /// <remarks>
    /// Built from the tag set instead of a hard-coded list of names, so a tag
    /// added later is covered without anyone remembering this file. There used
    /// to be an exclusion list here for [img] and [link], which this wrapper
    /// renders itself; it is gone because those two are no longer in the
    /// parser's tag set at all (see BbParserProvider), so nothing has to
    /// remember to exclude them.
    ///
    /// The value ends at the first quote followed by the closing bracket, which
    /// is the parser's own rule — [quote="a"b"] really does carry the value a"b.
    /// The name is matched case-sensitively for the same reason: [QUOTE="..."]
    /// is plain text to the parser, and encoding a value it will never
    /// substitute would show the entities to the reader.
    ///
    /// The unquoted alternative is not a convenience — it is what keeps this
    /// pass equal to the parser after the parser learned to read [quote=Vasya].
    /// Miss that spelling and the value goes to a tag declared secure: false,
    /// which substitutes it into its template as written, so
    /// [quote=&lt;img src=x onerror=alert(1)&gt;] would be a live event handler on
    /// the page of every reader. Both alternatives stop at an opening bracket,
    /// exactly as the parser's do, so an unterminated attribute costs one short
    /// scan instead of a scan of the whole remaining post — and because both
    /// stop there, the pass has to run on the string the parser will read and
    /// not on an earlier one (see the call site).
    /// </remarks>
    private static Regex? BuildAttributeTagPattern(IBbParser inner)
    {
        var names = inner.GetTags()
            .Where(tag => tag.WithAttribute)
            .Select(tag => Regex.Escape(tag.Name))
            .Distinct()
            .ToArray();

        return names.Length == 0
            ? null
            : new Regex(
                $@"\[({string.Join("|", names)})=(?:""(?>((?:[^""\[]|""(?!\]))*))""|(?>([^\[\]""\r\n]*)))\]",
                RegexOptions.Compiled);
    }

    /// <summary>
    /// HTML-encode the attribute value of every tag that puts it into the
    /// markup it emits.
    /// </summary>
    /// <remarks>
    /// The parser substitutes {value} as it stands. Declared secure: false it
    /// does not touch the value at all; declared secure: true it deletes spaces
    /// and quotes rather than encoding, which mangles an ordinary name — "A &amp; B"
    /// becomes "A&amp;B" — and still passes &lt;svg/onload=...&gt;, where no space is
    /// needed. Neither setting is an escape, and the value is author text, so
    /// [quote="&lt;img src=x onerror=alert(1)&gt;"] put a live event handler into the
    /// page of every reader on every surface.
    ///
    /// Encoding immediately before the parser is the single point that covers
    /// both positions the value lands in — element text (the quote author line,
    /// the addressee line) and attribute value (data-bb-addressees) — and it
    /// costs the reader nothing, because the browser decodes the entities back
    /// for display and for getAttribute. Brackets are left alone, so an [img]
    /// nested inside an attribute still renders as the element it is: harmless
    /// once a URL can no longer smuggle whitespace (see SanitizeUrl), and
    /// visible to everyone who sees the post.
    ///
    /// "Immediately" is load-bearing and not a figure of speech: this pass and
    /// the parser both end a value at an opening bracket, and while the pass ran
    /// earlier in the pipeline the extractions in between deleted brackets, so a
    /// value this refused to read became one the parser read. The call site
    /// carries the worked example.
    ///
    /// What it does change is the string the parser then reports as the
    /// attribute value, while the private-addressee snapshot is keyed by the raw
    /// text instead. The two ends are held together by
    /// <see cref="BbAttributeEncoding"/>, whose Decode the visibility filter runs
    /// before it looks a block up.
    /// </remarks>
    private string EncodeAttributeValues(string input) =>
        _attributeTagPattern is null
            ? input
            : _attributeTagPattern.Replace(input, match =>
            {
                // Group 2 is the quoted spelling, group 3 the unquoted one; the
                // output is quoted either way, so the parser reads one shape and
                // the encoding covers both.
                var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                return $"[{match.Groups[1].Value}=\"{BbAttributeEncoding.Encode(value)}\"]";
            });

    /// <summary>
    /// Create wrapper around existing parser
    /// </summary>
    /// <param name="inner">Parser handling every tag this wrapper does not</param>
    /// <param name="spoilerGatedImages">
    /// Whether an image is put behind a spoiler instead of embedding on sight.
    /// It is decided here and not by the tag set because this class renders [img]
    /// itself, before the inner parser ever sees the text: a "safe" tag set that
    /// swapped the img template decided nothing at all, and the guest-readable
    /// global chat auto-loaded whatever address a message named.
    /// </param>
    public BbParserWrapper(IBbParser inner, bool spoilerGatedImages = false)
    {
        _inner = inner;
        _attributeTagPattern = BuildAttributeTagPattern(inner);
        _spoilerGatedImages = spoilerGatedImages;
    }

    /// <inheritdoc />
    public NodeTree Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return _inner.Parse(input);

        // Extract [img], [link], and [mention] tags, replace with placeholders
        // Note: [spoiler=X] is NOT supported - only simple [spoiler] handled by BBCodeParser
        var imgList = new List<(string url, int? width, int? height, string? alt)>();
        var linkList = new List<(string? text, string url)>();
        var mentionList = new List<string>();

        // Before anything else, three passes over the raw text, in this order.
        // The marker goes first: nothing the author typed may be mistaken for a
        // placeholder written below (see PlaceholderMarker). Then [code] and
        // [noparse] are lifted out whole, because everything after this line
        // rewrites markup while those two exist to show it as written — which is
        // how [code][img]url[/img][/code] embedded the picture instead of
        // printing the tag, and how a [private=Name] inside a code sample got
        // rewritten in front of the reader. Then the privacy tag has to be
        // spelled the way the tag set recognises, or the visitor gets no node to
        // filter and private text is served to everyone (see PrivateBlockMarkup,
        // whose rules the save path keys its addressee snapshot by, so the two
        // ends cannot drift apart), and it has to happen before the extractions
        // below so that a block they would damage is already downgraded.
        //
        // Encoding is not here. It goes last, immediately before the parser —
        // see the call site for why that position is the whole of its
        // correctness.
        var verbatimList = new List<string>();
        var processed = VerbatimBlock.Replace(StripPlaceholderMarkers(input), match =>
        {
            var index = verbatimList.Count;
            verbatimList.Add(match.Value);
            return Placeholder(VerbatimKind, index);
        });

        processed = PrivateBlockMarkup.Normalise(processed);

        // Extract [img=WxH alt="text"]URL[/img] or [img=W alt="text"]URL[/img] (MUST be first)
        processed = ImgWithSizeAndAlt.Replace(processed, match =>
        {
            if (!MayExtract(match)) return match.Value;
            var width = int.TryParse(match.Groups[1].Value, out var w) ? w : (int?)null;
            var height = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var h) ? h : (int?)null;
            var alt = match.Groups[3].Value;
            var url = match.Groups[4].Value;
            var index = imgList.Count;
            imgList.Add((url, width, height, string.IsNullOrEmpty(alt) ? null : alt));
            return Placeholder(ImageKind, index);
        });

        // Extract [img=WxH]URL[/img] or [img=W]URL[/img] (no alt)
        processed = ImgWithSize.Replace(processed, match =>
        {
            if (!MayExtract(match)) return match.Value;
            var width = int.TryParse(match.Groups[1].Value, out var w) ? w : (int?)null;
            var height = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var h) ? h : (int?)null;
            var url = match.Groups[3].Value;
            var index = imgList.Count;
            imgList.Add((url, width, height, null));
            return Placeholder(ImageKind, index);
        });

        // Extract [img alt="text"]URL[/img] (alt only, no size)
        processed = ImgWithAlt.Replace(processed, match =>
        {
            if (!MayExtract(match)) return match.Value;
            var alt = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            var index = imgList.Count;
            imgList.Add((url, null, null, string.IsNullOrEmpty(alt) ? null : alt));
            return Placeholder(ImageKind, index);
        });

        // Extract [img]URL[/img] (simple, no size, no alt)
        processed = Img.Replace(processed, match =>
        {
            if (!MayExtract(match)) return match.Value;
            var url = match.Groups[1].Value;
            var index = imgList.Count;
            imgList.Add((url, null, null, null)); // null = use default max dimensions
            return Placeholder(ImageKind, index);
        });

        // Extract [img="URL"] / [img=URL] — the URL is the attribute, and this is
        // the form that used to reach the inner parser untouched.
        processed = ImgAttributeRegex().Replace(processed, match =>
        {
            if (!MayExtract(match)) return match.Value;
            var url = match.Groups[1].Value.Trim();
            var index = imgList.Count;
            imgList.Add((url, null, null, null));
            return Placeholder(ImageKind, index);
        });

        // Extract [link=text]URL[/link] (MUST be before simple link)
        processed = LinkWithText.Replace(processed, match =>
        {
            if (!MayExtract(match)) return match.Value;
            var text = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            var index = linkList.Count;
            linkList.Add((text, url));
            return Placeholder(LinkKind, index);
        });

        // Extract [link]URL[/link]
        processed = LinkSimple.Replace(processed, match =>
        {
            if (!MayExtract(match)) return match.Value;
            var url = match.Groups[1].Value;
            var index = linkList.Count;
            linkList.Add((null, url)); // null text = simple link
            return Placeholder(LinkKind, index);
        });

        // Extract [mention="username"] - standalone tag
        processed = Mention.Replace(processed, match =>
        {
            if (!MayExtract(match)) return match.Value;
            var username = match.Groups[1].Value;
            var index = mentionList.Count;
            mentionList.Add(username);
            return Placeholder(MentionKind, index);
        });

        // Encode the attribute values the parser will substitute into markup —
        // here, on the exact string the parser is about to be handed, and
        // nowhere earlier.
        //
        // The position is the correctness. Both this pass and the parser stop an
        // attribute value at an opening bracket, which reads like agreement, and
        // it was agreement — on two different strings. Run before the
        // extractions above, this pass saw
        //
        //     [quote=<img src=x onerror=alert(1)>[img]https://ex.com/a.png[/img]]
        //
        // gave up at the bracket and rewrote nothing; the extractions then
        // replaced the inner [img] with a placeholder, which carries no bracket,
        // and the parser read the value it had just refused. [quote] is declared
        // secure: false, so that value went into <div class="quote-author"> as
        // written and the client binds it through v-html: stored XSS on every
        // surface that renders a quotation. Any tag this wrapper extracts —
        // [link=x]url[/link], [mention="x"] — works the same way, because all of
        // them turn brackets into a placeholder that has none.
        //
        // With the pass moved here the two patterns read one string, so "the
        // parser reads a value this did not read" is not a rule anyone has to
        // maintain — it is unrepresentable. Still before the verbatim blocks come
        // back, because their content is shown as written and must not be
        // rewritten.
        processed = EncodeAttributeValues(processed);

        // Put [code] and [noparse] back before the parser: they are its tags, and
        // it is what renders them, verbatim content and all.
        processed = VerbatimPlaceholder.Replace(processed, match =>
            verbatimList[int.Parse(match.Groups[1].Value)]);

        // Parse the rest with BBCodeParser
        var innerTree = _inner.Parse(processed);

        // Return wrapped tree that restores placeholders
        return new WrappedNodeTree(innerTree, imgList, linkList, mentionList, _spoilerGatedImages);
    }

    /// <inheritdoc />
    public Tag[] GetTags() => _inner.GetTags();

    /// <summary>
    /// NodeTree wrapper that restores [img] and [link] placeholders in output.
    /// Public so BbConverter can cast to it and call the correct methods.
    ///
    /// Implementation note: placeholder indices go through int.Parse(), not
    /// TryParse, and that is safe rather than strict: the pattern captures (\d+)
    /// and the indices are generated from List.Count, so a non-numeric index
    /// cannot reach here. It is not a fail-fast guard either — an index past the
    /// end of its list is not thrown on, the handler returns the placeholder into
    /// the output as it stands.
    /// </summary>
    public class WrappedNodeTree : NodeTree
    {
        private readonly NodeTree _inner;
        private readonly List<(string url, int? width, int? height, string? alt)> _imgList;
        private readonly List<(string? text, string url)> _linkList;
        private readonly List<string> _mentionList;
        private readonly bool _spoilerGatedImages;

        private static readonly Regex ImgPlaceholder = PlaceholderPattern(ImageKind);
        private static readonly Regex LinkPlaceholder = PlaceholderPattern(LinkKind);
        private static readonly Regex MentionPlaceholder = PlaceholderPattern(MentionKind);

        /// <summary>
        /// Create wrapped node tree. Internal so the blanket assembly scan
        /// leaves the type alone: the inner tree is handed in by the wrapper,
        /// and nothing in the container answers for a NodeTree.
        /// </summary>
        internal WrappedNodeTree(NodeTree inner, List<(string url, int? width, int? height, string? alt)> imgList, List<(string? text, string url)> linkList, List<string> mentionList, bool spoilerGatedImages = false)
            : base(BbParser.SecuritySubstitutions, new Dictionary<string, string>())
        {
            _inner = inner;
            _imgList = imgList;
            _linkList = linkList;
            _mentionList = mentionList;
            _spoilerGatedImages = spoilerGatedImages;
        }

        /// <summary>
        /// Put an image behind a spoiler on the surfaces that ask for it.
        /// </summary>
        /// <remarks>
        /// The same markup an ordinary [spoiler] emits, because the client wires
        /// one behaviour to a .spoiler-head plus the .spoiler next to it, and a
        /// second shape would need a second implementation of the same toggle.
        /// </remarks>
        private string Gate(string image) => _spoilerGatedImages
            ? $"<a href=\"#\" class=\"spoiler-head\">{DefaultSpoilerText}</a>" +
              $"<div class=\"spoiler\">{image}</div>"
            : image;

        /// <summary>
        /// Convert to HTML with permission filter + transform applied during
        /// tree walk. Shares placeholder restoration with <see cref="ToHtml()"/>.
        /// </summary>
        public string ToHtmlFiltered(
            System.Func<Node, bool> filter,
            System.Func<Node, string, string> transform)
            => ToHtmlCore(_inner.ToHtml(filter, transform));

        /// <summary>
        /// Convert to plain text with permission filter + transform applied.
        /// </summary>
        public string ToTextFiltered(
            System.Func<Node, bool> filter,
            System.Func<Node, string, string> transform)
            => ToTextCore(_inner.ToText(filter, transform));

        /// <summary>
        /// Convert to HTML, restoring [img] and [link] as HTML elements
        /// </summary>
        public string ToHtml() => ToHtmlCore(_inner.ToHtml());

        private string ToHtmlCore(string html)
        {

            // Restore images as HTML — unified emission shared with frontend
            // bbcode.ts (see renderBbImage there). Sizing flows through CSS
            // custom properties on a wrapper <span class="bb-image-frame"> so
            // ancestor classes (TruncatedContent, etc.) can override image
            // sizes via normal cascade without !important. The <img> itself
            // carries NO inline style — only the wrapper span does, and only
            // when a custom size is explicitly set.
            //
            // Shapes:
            //   - Default: <img class="bb-image" data-bb-tag="img" ...>
            //   - Sized:   <span class="bb-image-frame" data-bb-width="W" data-bb-height="H"
            //                   style="--bb-image-max-width:Wpx;--bb-image-max-height:Hpx">
            //                <img class="bb-image" data-bb-tag="img" ...>
            //              </span>
            html = ImgPlaceholder.Replace(html, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _imgList.Count)
                {
                    var (url, width, height, alt) = _imgList[index];
                    var safeUrl = SanitizeUrl(url);
                    if (safeUrl == "#") return ""; // Remove dangerous image entirely
                    var encodedUrl = System.Web.HttpUtility.HtmlAttributeEncode(safeUrl);

                    // Build alt attribute (also add data-alt for frontend JS access)
                    var altAttr = " alt=\"\"";
                    if (!string.IsNullOrEmpty(alt))
                    {
                        var encodedAlt = System.Web.HttpUtility.HtmlAttributeEncode(alt);
                        altAttr = $" alt=\"{encodedAlt}\" data-alt=\"{encodedAlt}\"";
                    }

                    // loading/decoding: a page carries up to twenty posts, and images below
                    // the fold cost first paint for nothing. Both attributes are inert
                    // for anything already on screen.
                    var imgTag = $"<img src=\"{encodedUrl}\" class=\"bb-image\" " +
                                 $"data-bb-tag=\"img\" loading=\"lazy\" decoding=\"async\" " +
                                 $"referrerpolicy=\"no-referrer\"{altAttr} />";

                    if (!width.HasValue && !height.HasValue)
                    {
                        // Default size — no wrapper, CSS defaults on .bb-image take over
                        return Gate(imgTag);
                    }

                    // Custom size — wrap in .bb-image-frame span carrying the CSS vars
                    var cssVars = new List<string>();
                    var dataAttrs = new List<string>();
                    if (width.HasValue)
                    {
                        cssVars.Add($"--bb-image-max-width:{width.Value}px");
                        dataAttrs.Add($"data-bb-width=\"{width.Value}\"");
                    }
                    if (height.HasValue)
                    {
                        cssVars.Add($"--bb-image-max-height:{height.Value}px");
                        dataAttrs.Add($"data-bb-height=\"{height.Value}\"");
                    }
                    return Gate($"<span class=\"bb-image-frame\" {string.Join(" ", dataAttrs)} " +
                                $"style=\"{string.Join(";", cssVars)}\">{imgTag}</span>");
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
                    // Dangerous URL: drop the anchor but keep the text — encoded
                    // exactly like the accepted branch below. The text comes from
                    // [link=TEXT] and is attacker-controlled, so returning it raw
                    // turns a rejected URL into stored XSS.
                    if (safeUrl == "#") return System.Web.HttpUtility.HtmlEncode(text ?? "");
                    var encodedUrl = System.Web.HttpUtility.HtmlAttributeEncode(safeUrl);
                    var displayText = text ?? DefaultLinkText;
                    var encodedText = System.Web.HttpUtility.HtmlEncode(displayText);
                    return $"<a href=\"{encodedUrl}\" target=\"_blank\" rel=\"noopener\">{encodedText}</a>";
                }
                return match.Value;
            });

            // Restore mentions as HTML links
            html = MentionPlaceholder.Replace(html, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _mentionList.Count)
                {
                    var username = _mentionList[index];
                    var encodedUsername = System.Web.HttpUtility.HtmlAttributeEncode(username);
                    var encodedUsernameUrl = Uri.EscapeDataString(username);
                    var displayUsername = System.Web.HttpUtility.HtmlEncode(username);
                    return $"<a class=\"bb-mention\" href=\"/users/{encodedUsernameUrl}\" data-bb-tag=\"mention\" data-bb-user=\"{encodedUsername}\">@{displayUsername}</a>";
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
        public string ToText() => ToTextCore(_inner.ToText());

        private string ToTextCore(string text)
        {
            // Restore images as alt text (if available) or URL.
            //
            // An address the display path refuses answers here the way it answers
            // there: with nothing. The two walks emit the same content for the
            // same viewer, and the search text is built from this one - a URL that
            // no reader is ever shown is a word the index would hold and the page
            // could not account for.
            text = ImgPlaceholder.Replace(text, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _imgList.Count)
                {
                    var (url, _, _, alt) = _imgList[index];
                    if (!string.IsNullOrEmpty(alt)) return alt;
                    return SanitizeUrl(url) == "#" ? string.Empty : url;
                }
                return match.Value;
            });

            // Restore links as text or URL, refused addresses dropped for the
            // reason above - and the text kept, exactly as the HTML walk keeps it.
            text = LinkPlaceholder.Replace(text, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _linkList.Count)
                {
                    var (linkText, url) = _linkList[index];
                    if (linkText != null) return linkText;
                    return SanitizeUrl(url) == "#" ? string.Empty : url;
                }
                return match.Value;
            });

            // Restore mentions as @username
            text = MentionPlaceholder.Replace(text, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _mentionList.Count)
                {
                    return $"@{_mentionList[index]}";
                }
                return match.Value;
            });

            return text;
        }

        /// <summary>
        /// Convert back to BBCode in DM3 format
        /// </summary>
        public string ToBb() => ToBbCore(_inner.ToBb());

        /// <summary>
        /// Convert back to BBCode with the permission filter and the attribute
        /// transform applied during the tree walk. This is the source of a
        /// quotation; see <see cref="RenderAudience.QuoteSource"/>.
        /// </summary>
        public string ToBbFiltered(
            System.Func<Node, bool> filter,
            System.Func<Node, string, string> transform)
            => ToBbCore(_inner.ToBb(filter, transform));

        private string ToBbCore(string bb)
        {
            // Restore images as [img]URL[/img] or [img=WxH]URL[/img] or with alt.
            //
            // The address is judged by SanitizeUrl and the verdict acted on the
            // same way ToHtmlCore acts on it: an address the display path refuses
            // is not written here either. The two paths agreeing is not a
            // courtesy - it is the invariant that a source never shows a reader
            // more than the page would have shown the same reader. Written the
            // other way round, the quotation of a post with [img]javascript:...
            // handed the address back to whoever pressed the button while the
            // page it was quoted from showed nothing at all.
            bb = ImgPlaceholder.Replace(bb, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _imgList.Count)
                {
                    var (url, width, height, alt) = _imgList[index];
                    var safeUrl = SanitizeUrl(url);
                    if (safeUrl == "#") return ""; // Dropped whole, as on the display path
                    var altPart = !string.IsNullOrEmpty(alt) ? $" alt=\"{alt}\"" : "";

                    if (width.HasValue && height.HasValue)
                        return $"[img={width.Value}x{height.Value}{altPart}]{safeUrl}[/img]";
                    if (width.HasValue)
                        return $"[img={width.Value}{altPart}]{safeUrl}[/img]";
                    if (!string.IsNullOrEmpty(alt))
                        return $"[img{altPart}]{safeUrl}[/img]";
                    return $"[img]{safeUrl}[/img]";
                }
                return match.Value;
            });

            // Restore links as [link]URL[/link] or [link=text]URL[/link]. A
            // refused address leaves its text and loses its markup, which is
            // what the display path does with it.
            bb = LinkPlaceholder.Replace(bb, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _linkList.Count)
                {
                    var (text, url) = _linkList[index];
                    var safeUrl = SanitizeUrl(url);
                    if (safeUrl == "#") return text ?? "";
                    return text != null ? $"[link={text}]{safeUrl}[/link]" : $"[link]{safeUrl}[/link]";
                }
                return match.Value;
            });

            // Restore mentions as [mention="username"]
            bb = MentionPlaceholder.Replace(bb, match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                if (index < _mentionList.Count)
                {
                    return $"[mention=\"{_mentionList[index]}\"]";
                }
                return match.Value;
            });

            return bb;
        }
    }
}
