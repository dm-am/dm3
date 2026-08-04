using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Pipeline for avatars (UserAvatar, CharacterAvatar):
///   1) magic-byte format validation (client content-type is not trusted),
///   2) decompression-bomb protection (pre-decode pixel area check),
///   3) min/max dimension guards,
///   4) EXIF/IPTC/XMP strip (re-encode metadata-free),
///   5) downscale to <see cref="ImageProcessingDefaults.OriginalMaxDimension"/>
///      if the image is larger (Max-mode, aspect-preserving).
///
/// Returns a single file (source). Thumbnails are generated on-the-fly
/// via imgproxy at serving time — not pre-generated.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>True if the upload type requires the image pipeline (validation+EXIF strip).</summary>
    bool IsImageType(UploadType type);

    /// <summary>
    /// Read the stream, validate (magic bytes, dimensions, decompression
    /// bomb), strip EXIF, downscale if &gt;1024 px. Throws
    /// <see cref="DM.Domain.Core.Exceptions.HttpBadRequestException"/> on any
    /// validation error.
    /// </summary>
    Task<ProcessedImage> ProcessAsync(
        Stream input,
        string declaredContentType,
        CancellationToken ct = default);
}

/// <summary>
/// Processing result — a single re-encoded source file
/// ready for S3 PUT. Thumbnails are not pre-generated — imgproxy
/// does on-the-fly transforms on request.
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
    int Height);

/// <summary>
/// Publicly accessible pipeline constants — SSOT for docs, tests,
/// imgproxy presets.
/// </summary>
public static class ImageProcessingDefaults
{
    /// <summary>Maximum dimension of the source file after processing.</summary>
    public const int OriginalMaxDimension = 1024;
}
