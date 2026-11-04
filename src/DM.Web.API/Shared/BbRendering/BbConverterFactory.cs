using System;
using System.Collections.Generic;
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
/// into permission-aware rendered output at serialization time, or, for the
/// author loading their own text into the editor, into the stored BBCode as
/// typed. The factory
/// itself is a singleton (owned by <see cref="JsonSerializerOptions"/>);
/// the per-request scoped authorization context is
/// resolved through <see cref="HttpContext.RequestServices"/> at Write time.
/// </summary>
internal class BbConverterFactory(
    IHttpContextAccessor httpContextAccessor,
    IBbParserProvider bbParserProvider) : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsSubclassOf(typeof(BbText));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(BbConverter<>).MakeGenericType(typeToConvert),
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new object?[] { httpContextAccessor, bbParserProvider },
            null)!;

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
            if (raw.Length == 0)
            {
                writer.WriteStringValue(string.Empty);
                return;
            }

            var requestedAudience = bbText.FollowsAudience ? ReadAudienceHeader() : RenderAudience.Display;
            var viewer = ResolveViewerFromRequest();
            var audience = ResolveEffectiveAudience(requestedAudience, viewer, bbText.Context);

            // The editor works on BBCode: the author gets the source as typed,
            // not markup to be turned back into it.
            if (audience == RenderAudience.AuthorEdit)
            {
                writer.WriteStringValue(raw);
                return;
            }

            var renderContext = BuildRenderContext(bbText, audience, viewer);

            var parser = SelectParser(bbText, audience);

            try
            {
                var rendered = audience == RenderAudience.PlainText
                    ? parser.RenderText(raw, renderContext)
                    : parser.RenderHtml(raw, renderContext);

                writer.WriteStringValue(rendered);
            }
            catch (BbParserException)
            {
                // The parser refuses a tree nested deeper than BbParser.TreeMaxDepth,
                // mid response: one field renders as nothing and the page is served.
                // Not the source, which is unfiltered; its author got it above.
                writer.WriteStringValue(string.Empty);
            }
        }

        private RenderAudience ReadAudienceHeader()
        {
            var ctx = httpContextAccessor.HttpContext;
            if (ctx is null) return BbAudienceHeader.Default;
            if (!ctx.Request.Headers.TryGetValue(BbAudienceHeader.HeaderName, out var raw))
                return BbAudienceHeader.Default;
            return BbAudienceHeader.Parse(raw.ToString());
        }

        // The singleton factory holds no scoped service: the viewer comes from the
        // request's own scope, which the accessor exposes.
        private IAuthorizationSubject? ResolveViewerFromRequest()
        {
            var provider = httpContextAccessor.HttpContext?.RequestServices;
            if (provider is null) return null;
            try
            {
                return provider.GetService<IAuthorizationContextProvider>()?.CurrentSubject;
            }
            catch (ObjectDisposedException)
            {
                // Serialization outliving its request: render for a guest rather than fail the response.
                return null;
            }
        }

        /// <summary>
        /// Downgrades a client-requested AuthorEdit to Display unless the viewer
        /// is the content's author or may edit it. AuthorEdit reveals the unfiltered
        /// source (including [private]/[mod]); since the audience is a per-request
        /// header that applies to any endpoint, the right is verified at render
        /// time. Author identity and the edit check come from the mapping-populated
        /// envelope; a missing author id is treated as "not the author".
        /// </summary>
        /// <remarks>
        /// Both ids are real identities by the time they meet: the envelope drops an
        /// unfilled author id and AnonymousIdentity answers null for a guest, so an
        /// empty id never matches an empty id. Whether an editor who is not the
        /// author reads every [private] block of a game post is the edit right's own
        /// question (PostIntention.EditText): the edit right of moderation is not a
        /// right to read the blocks, so it does not reach such a post at all.
        /// </remarks>
        private RenderAudience ResolveEffectiveAudience(
            RenderAudience requested,
            IAuthorizationSubject? viewer,
            RenderContextEnvelope? envelope)
        {
            if (requested != RenderAudience.AuthorEdit)
                return requested;

            if (AnonymousIdentity.Of(viewer) is not Guid reader || envelope is null)
                return RenderAudience.Display;

            if (envelope.PostAuthorUserId == reader)
                return RenderAudience.AuthorEdit;

            if (envelope.EditableBy is { } mayEdit
                && ResolveIntentionManager() is { } intentions
                && mayEdit(intentions))
                return RenderAudience.AuthorEdit;

            return RenderAudience.Display;
        }

        private IIntentionManager? ResolveIntentionManager()
        {
            var provider = httpContextAccessor.HttpContext?.RequestServices;
            if (provider is null) return null;
            try
            {
                return provider.GetService<IIntentionManager>();
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        private static RenderContext BuildRenderContext(
            TBbText bbText,
            RenderAudience audience,
            IAuthorizationSubject? viewer)
        {
            var envelope = bbText.Context;
            var surface = envelope?.Surface ?? bbText.Surface;

            // Every audience cuts the files the viewer does not read: the header is the
            // caller's to choose, and a text view of a picture is its address.
            var withheld = envelope?.WithheldUploadIds ?? new HashSet<Guid>();

            if (audience == RenderAudience.PlainText)
                return RenderContext.ForPlainText() with { Surface = surface, WithheldUploadIds = withheld };

            if (audience == RenderAudience.EmbedSafe)
                return RenderContext.ForEmbedSafe(surface) with { WithheldUploadIds = withheld };

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
                GameShowPrivateMessages = envelope?.GameShowPrivateMessages ?? false,
                RoomViewPrivateText = envelope?.RoomViewPrivateText ?? false,
                WithheldUploadIds = withheld
            };
        }

        private BbParserWrapper SelectParser(TBbText bbText, RenderAudience audience)
        {
            var surface = bbText.Context?.Surface ?? bbText.Surface;

            return audience switch
            {
                // EmbedSafe uses the NSFW-safe variant for the surface.
                RenderAudience.EmbedSafe => bbParserProvider.GetSafeForSurface(surface),
                _ => bbParserProvider.GetForSurface(surface)
            };
        }
    }
}
