using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// The one pipeline every uploaded file goes through:
///   1) magic-byte format validation (client content-type is not trusted),
///   2) decompression-bomb protection (pre-decode pixel area check, frames included),
///   3) dimension guards,
///   4) EXIF/IPTC/XMP strip (re-encode metadata-free),
///   5) downscale to <see cref="ImageProcessingDefaults.OriginalMaxDimension"/>
///      if the type asks for it,
///   6) square thumbnails of an avatar (<see cref="AvatarThumbnail"/>).
///
/// Steps 3 and 5 differ per upload type — see <see cref="ImageProcessingDefaults"/>.
/// Never a thumbnail for a post attachment: its bytes are served by an endpoint
/// that authorizes the caller.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Read the stream, validate (magic bytes, dimensions, decompression bomb),
    /// strip EXIF, downscale where the type asks for it, cut the thumbnails of an
    /// avatar. Throws
    /// <see cref="DM.Domain.Core.Exceptions.HttpBadRequestException"/> on any
    /// validation error.
    /// </summary>
    /// <param name="input">Bytes as the caller sent them.</param>
    /// <param name="declaredContentType">
    /// What the client called the file. Recorded on the trace and never trusted:
    /// the format is decided by the magic bytes.
    /// </param>
    /// <param name="type">
    /// What the file is for. Decides the accepted formats, the dimension floor and
    /// whether the stored file is downscaled — an avatar is a small square and a
    /// post attachment is a map somebody has to be able to read.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<ProcessedImage> ProcessAsync(
        Stream input,
        string declaredContentType,
        UploadType type,
        CancellationToken ct = default);
}

/// <summary>
/// Processing result — the re-encoded source file ready for S3 PUT, and the
/// thumbnails that go next to it.
/// </summary>
/// <param name="Bytes">Encoded file to store.</param>
/// <param name="ContentType">MIME type the bytes are encoded in.</param>
/// <param name="Extension">Extension normalized from <paramref name="ContentType"/>.</param>
/// <param name="Width">
/// Width of <paramref name="Bytes"/> in pixels, after the optional downscale —
/// the intrinsic width of the file that ends up in the bucket, not of what the
/// caller sent. Recorded so a page can reserve the exact box an aspect-preserving
/// picture will occupy before the browser has decoded it.
/// </param>
/// <param name="Height">Height of <paramref name="Bytes"/> in pixels, on the same terms.</param>
public sealed record ProcessedImage(
    byte[] Bytes,
    string ContentType,
    string Extension,
    int Width,
    int Height)
{
    /// <summary>
    /// Square thumbnails of the stored picture, one per side
    /// <see cref="AvatarThumbnail.SizesFor"/> names for the type, none for the others.
    /// </summary>
    public IReadOnlyList<ProcessedThumbnail> Thumbnails { get; init; } = [];
}

/// <summary>
/// A square thumbnail of the stored picture, encoded as <see cref="AvatarThumbnail.ContentType"/>.
/// </summary>
/// <param name="Size">Side in pixels.</param>
/// <param name="Bytes">Encoded file to store.</param>
public sealed record ProcessedThumbnail(int Size, byte[] Bytes);

/// <summary>
/// Pipeline constants; the client's file picker keeps a copy.
/// </summary>
public static class ImageProcessingDefaults
{
    /// <summary>Maximum dimension of an avatar after processing.</summary>
    public const int OriginalMaxDimension = 1024;

    /// <summary>
    /// Smallest side an avatar may have. Junk-upload protection for a picture
    /// shown at a fixed small size; a post attachment has no floor, because a
    /// legend cut out of a map is a legitimate thing to attach.
    /// </summary>
    public const int AvatarMinDimension = 50;

    /// <summary>
    /// Formats an avatar may be in.
    /// </summary>
    public static IReadOnlyCollection<string> AvatarContentTypes { get; } =
        ["image/jpeg", "image/png", "image/webp"];

    /// <summary>
    /// Formats a post attachment may be in: pictures only, and only the ones the
    /// decoder in this pipeline reads.
    /// </summary>
    /// <remarks>
    /// Only formats validated by decoding: a document has no decode step, and stored
    /// as is it would be served from the site's own origin.
    /// </remarks>
    public static IReadOnlyCollection<string> PostAttachmentContentTypes { get; } =
        [.. AvatarContentTypes, "image/gif"];
}
