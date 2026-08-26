using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BBCodeParser;
using DM.Domain.Core.Authorization;
using DM.Infrastructure.Core.Parsing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// JSON converter factory that turns every <see cref="BbText"/>-derived DTO
/// into permission-aware rendered output at serialization time. The factory
/// itself is a singleton (owned by <see cref="JsonSerializerOptions"/>);
/// the per-request scoped authorization context is
/// resolved through <see cref="HttpContext.RequestServices"/> at Write time.
/// </summary>
internal class BbConverterFactory : JsonConverterFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IBbParserProvider _bbParserProvider;

    public BbConverterFactory(
        IHttpContextAccessor httpContextAccessor,
        IBbParserProvider bbParserProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _bbParserProvider = bbParserProvider;
    }

    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsSubclassOf(typeof(BbText));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converter = (JsonConverter)Activator.CreateInstance(
            typeof(BbConverter<>).MakeGenericType(typeToConvert),
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new object?[] { _httpContextAccessor, _bbParserProvider },
            null)!;
        return converter;
    }

    private class BbConverter<TBbText>(
        IHttpContextAccessor httpContextAccessor,
        IBbParserProvider bbParserProvider)
        : JsonConverter<TBbText>
        where TBbText : BbText, new()
    {

        public override TBbText Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => new() { Value = reader.GetString() ?? string.Empty };

        public override void Write(
            Utf8JsonWriter writer,
            TBbText bbText,
            JsonSerializerOptions options)
        {
            var raw = bbText.Value ?? string.Empty;
            var requestedAudience = ReadAudienceHeader();
            var viewer = ResolveViewerFromRequest();
            // AuthorEdit emits unfiltered round-trip source (every [private] and
            // [mod] block). The audience arrives as a client-controlled header
            // applied to every response, so authorship is enforced here rather
            // than trusted from the endpoint: AuthorEdit is honored only when the
            // viewer is the content's author, otherwise it degrades to Display.
            var audience = ResolveEffectiveAudience(requestedAudience, viewer, bbText.Context);
            var renderContext = BuildRenderContext(bbText, audience, viewer);

            // Fast path for a missing or empty input.
            if (raw.Length == 0)
            {
                writer.WriteStringValue(string.Empty);
                return;
            }

            var parser = SelectParser(bbText, audience);

            try
            {
                if (parser is not BbParserWrapper wrapper)
                {
                    // Shouldn't happen — every registered parser is a wrapper —
                    // but keep a safe fallback.
                    writer.WriteStringValue(ParseLegacy(parser, raw, audience));
                    return;
                }

                var rendered = audience == RenderAudience.PlainText
                    ? wrapper.RenderText(raw, renderContext)
                    : wrapper.RenderHtml(raw, renderContext);

                writer.WriteStringValue(rendered);
            }
            catch (BbParserException)
            {
                writer.WriteStringValue(Unrenderable(raw, audience));
            }
        }

        /// <summary>
        /// What one piece of content renders as when the parser refuses it.
        /// </summary>
        /// <remarks>
        /// The parser refuses exactly one thing: a tree nested deeper than it
        /// will build (see BbParser.TreeMaxDepth). Before the tree walk was made
        /// iterative that input did not raise anything catchable — it exhausted
        /// the stack and killed the process, which took the whole API down and
        /// took it down again on every later request that touched the same post.
        /// Now it arrives here, and it arrives mid-object: this converter runs
        /// inside a Utf8JsonWriter, so letting it out truncates the response
        /// body of an otherwise fine page. One field renders as nothing and the
        /// page is served.
        ///
        /// Nothing rather than the source text, because the source is not
        /// filtered: the visibility filter lives in the tree walk that just
        /// refused to run, so echoing the raw BBCode would hand every [private]
        /// block in it to whoever asked. The author is the exception — the
        /// audience only reaches AuthorEdit after the viewer has been matched
        /// against the author id — and the author needs the source back, or a
        /// post that cannot render is also a post that cannot be repaired.
        /// </remarks>
        private static string Unrenderable(string raw, RenderAudience audience) =>
            audience == RenderAudience.AuthorEdit
                ? WebUtility.HtmlEncode(raw)
                : string.Empty;

        private RenderAudience ReadAudienceHeader()
        {
            var ctx = httpContextAccessor.HttpContext;
            if (ctx is null) return BbAudienceHeader.Default;
            if (!ctx.Request.Headers.TryGetValue(BbAudienceHeader.HeaderName, out var raw))
                return BbAudienceHeader.Default;
            return BbAudienceHeader.Parse(raw.ToString());
        }

        private IAuthorizationSubject? ResolveViewerFromRequest()
        {
            // Scoped services (authorization context) must be resolved from
            // the current request's service scope, not from the singleton
            // factory's captured references. HttpContextAccessor bridges
            // the gap — it's a singleton but exposes the request-scoped
            // service provider.
            var provider = httpContextAccessor.HttpContext?.RequestServices;
            if (provider is null) return null;
            try
            {
                return provider.GetService<IAuthorizationContextProvider>()?.CurrentSubject;
            }
            catch (ObjectDisposedException)
            {
                // Serialization outliving its request: the accessor still hands
                // out an HttpContext whose scope has been disposed, and
                // resolving anything from it throws. Render as an anonymous
                // viewer rather than fail the response.
                //
                // The service being absent is not this case and never was: an
                // unregistered IAuthorizationContextProvider comes back as null
                // from GetService, and the ?. above already handles that.
                return null;
            }
        }

        /// <summary>
        /// Downgrades a client-requested AuthorEdit to Display unless the viewer
        /// is the content's author. AuthorEdit reveals unfiltered round-trip
        /// source (including [private]/[mod]); since the audience is a per-request
        /// header that applies to any endpoint, authorship is verified at render
        /// time. Author identity comes from the mapping-populated envelope; a
        /// missing author id is treated as "not the author" and denied.
        /// </summary>
        /// <remarks>
        /// Both sides of the comparison have to be a real identity, and neither
        /// is checked here: the envelope turns an unfilled author id into null
        /// on the way in, and AnonymousIdentity answers null for a reader who is
        /// not signed in. Compared raw, an anonymous reader's empty id equalled
        /// the empty id of any envelope whose author a projection had left
        /// unset, and the header alone then returned the unfiltered source of
        /// content on any surface.
        /// </remarks>
        private static RenderAudience ResolveEffectiveAudience(
            RenderAudience requested,
            IAuthorizationSubject? viewer,
            RenderContextEnvelope? envelope)
        {
            if (requested != RenderAudience.AuthorEdit)
                return requested;

            if (AnonymousIdentity.Of(viewer) is Guid reader
                && envelope?.PostAuthorUserId is Guid author
                && reader == author)
                return RenderAudience.AuthorEdit;

            return RenderAudience.Display;
        }

        private RenderContext BuildRenderContext(
            TBbText bbText,
            RenderAudience audience,
            IAuthorizationSubject? viewer)
        {
            var envelope = bbText.Context;
            var surface = envelope?.Surface ?? bbText.Surface;

            if (audience == RenderAudience.PlainText)
                return RenderContext.ForPlainText() with { Surface = surface };

            if (audience == RenderAudience.EmbedSafe)
                return RenderContext.ForEmbedSafe(surface);

            if (audience == RenderAudience.AuthorEdit && viewer is not null)
                return RenderContext.ForAuthorEdit(viewer, surface);

            var privateMap = envelope?.PrivateAddresseeOwnerUserIdsByAttribute
                             ?? new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);
            var privateNames = envelope?.PrivateAddresseeNamesByAttribute
                               ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            return new RenderContext
            {
                Viewer = viewer,
                Audience = audience,
                Surface = surface,
                PostAuthorUserId = envelope?.PostAuthorUserId,
                GameId = envelope?.GameId,
                PrivateAddresseeOwnerUserIdsByAttribute = privateMap,
                PrivateAddresseeNamesByAttribute = privateNames,
                GameLeadUserIds = envelope?.GameLeadUserIds ?? Array.Empty<Guid>(),
                PostSharePrivateWithAll = envelope?.PostSharePrivateWithAll ?? false,
                RoomViewPrivateText = envelope?.RoomViewPrivateText ?? false
            };
        }

        private BBCodeParser.IBbParser SelectParser(TBbText bbText, RenderAudience audience)
        {
            var surface = bbText.Context?.Surface ?? bbText.Surface;

            return audience switch
            {
                // EmbedSafe uses the NSFW-safe variant for the surface.
                RenderAudience.EmbedSafe => bbParserProvider.GetSafeForSurface(surface),
                // AuthorEdit uses the round-trip variant emitting data-bb-*.
                RenderAudience.AuthorEdit => bbParserProvider.GetForAuthorEdit(surface),
                _ => bbParserProvider.GetForSurface(surface)
            };
        }

        private static string ParseLegacy(BBCodeParser.IBbParser parser, string raw, RenderAudience audience)
        {
            var tree = parser.Parse(raw);
            if (tree is BbParserWrapper.WrappedNodeTree wrapped)
                return audience switch
                {
                    RenderAudience.PlainText => wrapped.ToText(),
                    _ => wrapped.ToHtml()
                };
            return audience switch
            {
                RenderAudience.PlainText => tree.ToText(),
                _ => tree.ToHtml()
            };
        }
    }
}
