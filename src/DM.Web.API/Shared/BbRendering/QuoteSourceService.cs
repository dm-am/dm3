using System;
using System.Collections.Generic;
using System.Net;
using BBCodeParser;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Shared.BbRendering;

/// <inheritdoc />
internal class QuoteSourceService : IQuoteSourceService
{
    private readonly IBbParserProvider _bbParserProvider;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;

    /// <summary>
    /// The one thing a reader is told when the text refuses to parse at all.
    /// </summary>
    /// <remarks>
    /// The parser refuses exactly one input: a tree nested deeper than it will
    /// build. The page shows such a post as nothing, and a quotation of nothing
    /// is a header with an empty body - which reads as a bug rather than as a
    /// refusal, so it is said out loud instead.
    /// </remarks>
    private const string UnquotableMessage = "Это сообщение не удалось разобрать, цитата не собрана";

    public QuoteSourceService(
        IBbParserProvider bbParserProvider,
        IAuthorizationContextProvider authorizationContextProvider)
    {
        _bbParserProvider = bbParserProvider;
        _authorizationContextProvider = authorizationContextProvider;
    }

    /// <inheritdoc />
    public Envelope<QuoteSource> Build(BbText text, string? authorName)
    {
        var raw = text.Value ?? string.Empty;
        var envelope = text.Context;
        var surface = envelope?.Surface ?? text.Surface;

        if (raw.Length == 0)
        {
            return new Envelope<QuoteSource>(new QuoteSource
            {
                Text = QuoteBlockMarkup.Compose(string.Empty, authorName),
                PrivateTextStripped = false
            });
        }

        var parser = _bbParserProvider.GetForSurface(surface);
        if (parser is not BbParserWrapper wrapper)
        {
            // Cannot happen - every registered parser is a wrapper - and the
            // answer if it ever does is not a half-filtered quotation.
            throw new HttpException(HttpStatusCode.BadRequest, UnquotableMessage);
        }

        QuoteSourceResult result;
        try
        {
            result = wrapper.RenderQuoteSource(raw, BuildRenderContext(surface, envelope));
        }
        catch (BbParserException)
        {
            throw new HttpException(HttpStatusCode.BadRequest, UnquotableMessage);
        }

        return new Envelope<QuoteSource>(new QuoteSource
        {
            Text = QuoteBlockMarkup.Compose(result.Source, authorName),
            PrivateTextStripped = result.PrivateTextStripped
        });
    }

    /// <summary>
    /// The same provenance the display render is given, for the same reader.
    /// </summary>
    /// <remarks>
    /// The audience is not set here - <see cref="BbParserWrapper.RenderQuoteSource"/>
    /// overrides it - and the rest is filled in for a reason that is not obvious:
    /// the quotation strips every private block anyway, so none of these fields
    /// changes the text. They decide the second answer instead, the one about
    /// whether what was stripped is something this reader would have been shown.
    /// Left unfilled, that answer is "no" for everybody, including the author of
    /// the post, who would then lose a line of his own post without a word.
    /// </remarks>
    private RenderContext BuildRenderContext(BbSurface surface, RenderContextEnvelope? envelope) => new()
    {
        Viewer = _authorizationContextProvider.CurrentSubject,
        Audience = RenderAudience.QuoteSource,
        Surface = surface,
        PostAuthorUserId = envelope?.PostAuthorUserId,
        GameId = envelope?.GameId,
        PrivateAddresseeOwnerUserIdsByAttribute = envelope?.PrivateAddresseeOwnerUserIdsByAttribute
                                                  ?? new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal),
        GameLeadUserIds = envelope?.GameLeadUserIds ?? Array.Empty<Guid>(),
        PostSharePrivateWithAll = envelope?.PostSharePrivateWithAll ?? false,
        RoomViewPrivateText = envelope?.RoomViewPrivateText ?? false
    };
}
