using System;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// A file attached to a post, as a reader of the post sees it.
/// </summary>
/// <remarks>
/// No object key and no address: the endpoint authorizes every request, and a direct link would be a pass.
/// </remarks>
public class PostAttachment
{
    /// <summary>What the content endpoint is asked for.</summary>
    public Guid Id { get; set; }

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    /// <summary>
    /// Pixels, so a page reserves the box before the picture is decoded; null when unknown.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>Intrinsic height in pixels; travels with <see cref="Width"/>.</summary>
    public int? Height { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Who sees the file: null for every reader of the post, else the addressees chosen
    /// (PrivateAddresseeSnapshot.ReadList).
    /// </summary>
    public string? AddresseesJson { get; set; }
}
