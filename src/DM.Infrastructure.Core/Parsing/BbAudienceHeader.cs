using System;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// The header a client names its <see cref="RenderAudience"/> in. An unknown value
/// falls back to <see cref="Default"/>.
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
            "author_edit" => RenderAudience.AuthorEdit,
            "plain_text" => RenderAudience.PlainText,
            "embed_safe" => RenderAudience.EmbedSafe,
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
