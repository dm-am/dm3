using System;

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

    /// <summary>Parse a raw header value into a <see cref="RenderAudience"/>.</summary>
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
    public static string Serialize(RenderAudience audience) => audience switch
    {
        RenderAudience.Display => "display",
        RenderAudience.AuthorEdit => "author_edit",
        RenderAudience.PlainText => "plain_text",
        RenderAudience.EmbedSafe => "embed_safe",
        _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, null)
    };
}
