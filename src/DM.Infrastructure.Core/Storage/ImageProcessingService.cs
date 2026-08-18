using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Tracing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class ImageProcessingService : IImageProcessingService
{
    // --- Pipeline constants (SSOT for sizes and limits) ---
    /// <summary>Maximum dimension of the original; anything larger is downscaled.</summary>
    public const int OriginalMaxDimension = ImageProcessingDefaults.OriginalMaxDimension;
    /// <summary>Minimum source size of an avatar (protection from junk uploads).</summary>
    public const int MinDimension = ImageProcessingDefaults.AvatarMinDimension;
    /// <summary>Maximum source size on any side (decompression-bomb protection after decode).</summary>
    public const int MaxDimension = 8192;
    /// <summary>
    /// Maximum decoded image area (W*H pixels) —
    /// protection from decompression-bomb attacks (a 1 KB PNG expanding
    /// to 50000×50000). 67 million pixels is the square of MaxDimension
    /// (8192×8192), not 8K UHD — that is 33 million.
    /// </summary>
    public const long MaxDecodedPixels = 64L * 1024 * 1024;

    // --- Extension per detected format (SSOT; the extension a stored key gets) ---
    private static readonly Dictionary<string, string> ExtensionByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    /// <summary>
    /// What each upload type accepts and what the pipeline does to it.
    /// </summary>
    /// <param name="ContentTypes">Formats admitted, decided by magic bytes.</param>
    /// <param name="MinDimension">Smallest permitted side, 0 for no floor.</param>
    /// <param name="MaxStoredDimension">
    /// Largest side of the stored file, null to store what was sent at its own
    /// size.
    /// </param>
    private sealed record TypeRules(
        IReadOnlyCollection<string> ContentTypes,
        int MinDimension,
        int? MaxStoredDimension);

    /// <summary>
    /// Per-type rules. An avatar is shown at a fixed small size, so it is floored
    /// and downscaled; an attachment is a map or a scheme whose detail is the
    /// point, so it keeps its own dimensions and is only re-encoded.
    /// </summary>
    private static readonly Dictionary<UploadType, TypeRules> RulesByType = new()
    {
        [UploadType.UserAvatar] = new TypeRules(
            ImageProcessingDefaults.AvatarContentTypes,
            ImageProcessingDefaults.AvatarMinDimension,
            ImageProcessingDefaults.OriginalMaxDimension),
        [UploadType.CharacterAvatar] = new TypeRules(
            ImageProcessingDefaults.AvatarContentTypes,
            ImageProcessingDefaults.AvatarMinDimension,
            ImageProcessingDefaults.OriginalMaxDimension),
        [UploadType.PostAttachment] = new TypeRules(
            ImageProcessingDefaults.PostAttachmentContentTypes,
            MinDimension: 0,
            MaxStoredDimension: null),
    };

    /// <inheritdoc />
    /// <remarks>
    /// Every upload type the product has is an image, and every one of them goes
    /// through this pipeline.
    /// </remarks>
    public bool IsImageType(UploadType type) =>
        type is UploadType.UserAvatar or UploadType.CharacterAvatar or UploadType.PostAttachment;

    /// <inheritdoc />
    public async Task<ProcessedImage> ProcessAsync(
        Stream input,
        string declaredContentType,
        UploadType type,
        CancellationToken ct = default)
    {
        if (!RulesByType.TryGetValue(type, out var rules))
        {
            // Fails closed: a type added to the enum without a rule refuses rather
            // than inheriting whichever set happens to be first.
            throw new InvalidOperationException($"No image processing rules for {type}");
        }

        using var activity = DmActivitySource.Source.StartActivity(
            "image.process",
            ActivityKind.Internal);
        activity?.SetTag("image.declared_content_type", declaredContentType);
        activity?.SetTag("image.upload_type", type.ToString());

        // 1. Magic-byte detection: do NOT trust the client-provided content-type.
        var buffered = await BufferStreamAsync(input, ct);
        activity?.SetTag("image.input_size_bytes", buffered.Length);

        IImageFormat format;
        try
        {
            format = await Image.DetectFormatAsync(new MemoryStream(buffered, writable: false), ct);
        }
        catch (UnknownImageFormatException)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = "Файл не является изображением",
            });
        }

        var actualContentType = format.DefaultMimeType;
        if (!rules.ContentTypes.Contains(actualContentType) ||
            !ExtensionByContentType.TryGetValue(actualContentType, out var normalizedExtension))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = $"Допустимые форматы: {FormatList(rules.ContentTypes)}",
            });
        }

        // 2. Identify-only pass: dimensions without a full decode (cheap).
        ImageInfo info;
        try
        {
            info = await Image.IdentifyAsync(new MemoryStream(buffered, writable: false), ct);
        }
        catch (Exception)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = "Не удалось прочитать изображение",
            });
        }

        if (rules.MinDimension > 0 &&
            (info.Width < rules.MinDimension || info.Height < rules.MinDimension))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = $"Изображение слишком маленькое (минимум {rules.MinDimension}x{rules.MinDimension})",
            });
        }

        if (info.Width > MaxDimension || info.Height > MaxDimension)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = $"Изображение слишком большое (максимум {MaxDimension}x{MaxDimension})",
            });
        }

        // 3. Decompression-bomb protection: total area before decoding, times the
        //    number of frames. GIF and WebP carry animations, and every frame is
        //    decoded into memory at full size, so a file whose single frame is
        //    within budget can still be a bomb a thousand times over.
        var frameCount = Math.Max(1, info.FrameMetadataCollection.Count);
        var pixelCount = (long)info.Width * info.Height * frameCount;
        if (pixelCount > MaxDecodedPixels)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = "Изображение содержит слишком много пикселей",
            });
        }

        // 4. Full decode (safe now — dimensions are verified).
        using var image = await Image.LoadAsync(new MemoryStream(buffered, writable: false), ct);

        // 5. Downscale where the type asks for it (Max-mode preserves aspect ratio).
        //    This is ALWAYS a re-encode → EXIF metadata is stripped automatically
        //    (ImageSharp does not write EXIF on SaveAs* by default).
        //
        //    A post attachment asks for nothing here and is stored at the size it
        //    arrived: the reason to attach a map or a battle plan is the detail in
        //    it, and 1024 px on the long side is where that detail stops being
        //    legible. It is still re-encoded — that is what strips the GPS
        //    coordinates out of a photographed sheet of paper, and what makes the
        //    stored bytes a file this decoder produced rather than whatever the
        //    caller wrapped around an image header.
        var maxStored = rules.MaxStoredDimension;
        if (maxStored.HasValue &&
            (image.Width > maxStored.Value || image.Height > maxStored.Value))
        {
            image.Mutate(c => c.Resize(new ResizeOptions
            {
                Size = new Size(maxStored.Value, maxStored.Value),
                Mode = ResizeMode.Max,
            }));
        }

        // EXIF/IPTC/XMP are stripped explicitly — even if pictures were <1024,
        // we re-encode the original to drop GPS, serial numbers, etc.
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        var bytes = await EncodeAsync(image, actualContentType, ct);
        activity?.SetTag("image.output_size_bytes", bytes.Length);
        activity?.SetTag("image.output_width", image.Width);
        activity?.SetTag("image.output_height", image.Height);

        // Dimensions of the image as it is being encoded, i.e. after the resize
        // above — the identify pass at step 2 measured what arrived, and for
        // anything over 1024 px that is not what gets stored.
        return new ProcessedImage(
            Bytes: bytes,
            ContentType: actualContentType,
            Extension: normalizedExtension,
            Width: image.Width,
            Height: image.Height);
    }

    private static async Task<byte[]> EncodeAsync(Image image, string contentType, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        // Re-encode in the source format — no silent conversion.
        // EXIF/IPTC/XMP are already cleared in Metadata.
        switch (contentType)
        {
            case "image/jpeg":
                await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 }, ct);
                break;
            case "image/png":
                await image.SaveAsPngAsync(ms, new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression,
                }, ct);
                break;
            case "image/webp":
                await image.SaveAsWebpAsync(ms, new WebpEncoder
                {
                    Quality = 90,
                    FileFormat = WebpFileFormatType.Lossy,
                }, ct);
                break;
            case "image/gif":
                // Palette-based and lossless by construction; the encoder keeps
                // every frame, so an animation survives the metadata strip.
                await image.SaveAsGifAsync(ms, new GifEncoder(), ct);
                break;
            default:
                // unreachable — guarded above
                throw new InvalidOperationException($"Unsupported content type {contentType}");
        }
        return ms.ToArray();
    }

    /// <summary>
    /// The accepted formats as a refusal names them: "JPEG, PNG, WebP".
    /// </summary>
    /// <remarks>
    /// Derived from the list the type actually enforces rather than written out
    /// beside it, so a format added to one and not the other cannot happen — the
    /// message used to say "JPEG, PNG, WebP" from a literal while the whitelist
    /// was a dictionary a few lines up.
    /// </remarks>
    private static string FormatList(IReadOnlyCollection<string> contentTypes) =>
        string.Join(", ", contentTypes.Select(DisplayName));

    private static string DisplayName(string contentType) => contentType switch
    {
        "image/jpeg" => "JPEG",
        "image/png" => "PNG",
        "image/webp" => "WebP",
        "image/gif" => "GIF",
        _ => contentType,
    };

    private static async Task<byte[]> BufferStreamAsync(Stream input, CancellationToken ct)
    {
        if (input is MemoryStream ms)
        {
            return ms.ToArray();
        }
        await using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
