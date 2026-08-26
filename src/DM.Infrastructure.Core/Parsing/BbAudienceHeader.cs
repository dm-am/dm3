using System;
using System.Collections.Generic;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// HTTP contract for selecting the rendering audience. Clients pass
/// the desired intent in the <see cref="HeaderName"/> header; the
/// server resolves it to a <see cref="RenderAudience"/> and renders
/// BbText fields accordingly. Unknown header values fall back to
/// <see cref="Default"/> (fail-safe to Display).
/// </summary>
public static class BbAudienceHeader
{
    /// <summary>HTTP header name.</summary>
    public const string HeaderName = "X-Dm-Audience";

    /// <summary>Default audience when the header is missing or unknown.</summary>
    public const RenderAudience Default = RenderAudience.Display;

    /// <summary>
    /// Every audience a client may name, and the only ones with a wire value.
    /// </summary>
    /// <remarks>
    /// Not <c>Enum.GetValues</c>: the enum also carries
    /// <see cref="RenderAudience.QuoteSource"/>, which is chosen by the endpoint
    /// that composes a quotation and by nobody else. The pair below is held to
    /// <see cref="Parse"/> and <see cref="Serialize"/> by a test, so a new
    /// audience cannot become client-selectable by being added to the enum.
    /// </remarks>
    public static IReadOnlyList<RenderAudience> Wire { get; } =
    [
        RenderAudience.Display,
        RenderAudience.AuthorEdit,
        RenderAudience.PlainText,
        RenderAudience.EmbedSafe
    ];

    /// <summary>Parse a raw header value into a <see cref="RenderAudience"/>.</summary>
    /// <remarks>
    /// <see cref="RenderAudience.QuoteSource"/> has no spelling here and must
    /// not get one. It emits BBCode source, which carries author text no
    /// security substitution has touched; reachable through a header that
    /// applies to every BbText field of every response, it would put that text
    /// into fields the client binds through v-html. The quotation endpoint
    /// selects it directly.
    /// </remarks>
    public static RenderAudience Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Default;
        return raw.Trim().ToLowerInvariant() switch
        {
            "display" => RenderAudience.Display,
            "author_edit" or "authoredit" => RenderAudience.AuthorEdit,
            "plain_text" or "plaintext" or "text" => RenderAudience.PlainText,
            "embed_safe" or "embedsafe" or "safehtml" => RenderAudience.EmbedSafe,
            _ => Default
        };
    }

    /// <summary>Canonical wire value for a given audience.</summary>
    /// <remarks>
    /// <see cref="RenderAudience.QuoteSource"/> has no wire value and falls to
    /// the throw below on purpose: it is not an audience a client may ask for.
    /// </remarks>
    public static string Serialize(RenderAudience audience) => audience switch
    {
        RenderAudience.Display => "display",
        RenderAudience.AuthorEdit => "author_edit",
        RenderAudience.PlainText => "plain_text",
        RenderAudience.EmbedSafe => "embed_safe",
        _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, null)
    };
}
