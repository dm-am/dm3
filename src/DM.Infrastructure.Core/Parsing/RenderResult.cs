#nullable enable
namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// Output of the permission-aware rendering pipeline. Carries the
/// emitted representation plus metadata the caller needs for caching,
/// metrics, and diagnostics.
/// </summary>
public sealed record RenderResult
{
    /// <summary>Rendered HTML. Empty string for PlainText audience.</summary>
    public required string Html { get; init; }

    /// <summary>Plain-text projection of the content. Non-null for PlainText
    /// audience; may be null for Display/AuthorEdit.</summary>
    public string? PlainText { get; init; }

    /// <summary>Permission bucket used to produce this output — identical
    /// bucket yields identical output for identical source.</summary>
    public required PermissionBucket Bucket { get; init; }

    /// <summary>Stable short hash of the raw BBCode input, used as cache
    /// invalidation key when the source is edited.</summary>
    public required string SourceHash { get; init; }
}
