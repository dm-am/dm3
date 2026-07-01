using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Tracing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class ImageProcessingService : IImageProcessingService
{
    // --- Pipeline constants (SSOT для размеров и лимитов) ---
    /// <summary>Максимальная сторона оригинала; все больше — downscale-ится.</summary>
    public const int OriginalMaxDimension = ImageProcessingDefaults.OriginalMaxDimension;
    /// <summary>Min-размер исходника (защита от мусорных upload'ов).</summary>
    public const int MinDimension = 50;
    /// <summary>Max-размер исходника по любой стороне (защита от decompression-bomb после декода).</summary>
    public const int MaxDimension = 8192;
    /// <summary>
    /// Максимальная площадь декодированного изображения (W*H пикселей) —
    /// защита от decompression-bomb атак (1 KB PNG, разворачивающийся
    /// в 50000×50000). 67 миллионов пикселей ≈ 8K resolution.
    /// </summary>
    public const long MaxDecodedPixels = 64L * 1024 * 1024;

    // --- Format whitelist + magic-byte mapping (SSOT) ---
    private static readonly Dictionary<string, string> ExtensionByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    /// <inheritdoc />
    public bool IsImageType(UploadType type) =>
        type is UploadType.UserAvatar or UploadType.CharacterAvatar;

    /// <inheritdoc />
    public async Task<ProcessedImage> ProcessAsync(
        Stream input,
        string declaredContentType,
        CancellationToken ct = default)
    {
        using var activity = DmActivitySource.Source.StartActivity(
            "image.process",
            ActivityKind.Internal);
        activity?.SetTag("image.declared_content_type", declaredContentType);

        // 1. Magic-byte detection: НЕ доверяем client-provided content-type.
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
        if (!ExtensionByContentType.TryGetValue(actualContentType, out var normalizedExtension))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = "Допустимые форматы: JPEG, PNG, WebP",
            });
        }

        // 2. Identify-only пас: размеры без полного декода (cheap).
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

        if (info.Width < MinDimension || info.Height < MinDimension)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = $"Изображение слишком маленькое (минимум {MinDimension}x{MinDimension})",
            });
        }

        if (info.Width > MaxDimension || info.Height > MaxDimension)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = $"Изображение слишком большое (максимум {MaxDimension}x{MaxDimension})",
            });
        }

        // 3. Decompression-bomb защита: суммарная площадь до декода.
        var pixelCount = (long)info.Width * info.Height;
        if (pixelCount > MaxDecodedPixels)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = "Изображение содержит слишком много пикселей",
            });
        }

        // 4. Полный декод (теперь безопасный — размеры проверены).
        using var image = await Image.LoadAsync(new MemoryStream(buffered, writable: false), ct);

        // 5. Downscale если >1024 (Max-mode сохраняет aspect-ratio).
        //    Это ВСЕГДА re-encode → EXIF metadata автоматически stripped
        //    (ImageSharp по умолчанию не пишет EXIF при SaveAs*).
        if (image.Width > OriginalMaxDimension || image.Height > OriginalMaxDimension)
        {
            image.Mutate(c => c.Resize(new ResizeOptions
            {
                Size = new Size(OriginalMaxDimension, OriginalMaxDimension),
                Mode = ResizeMode.Max,
            }));
        }

        // EXIF/IPTC/XMP стрипаются явно — даже если pictures были <1024,
        // мы re-encode'аем оригинал чтобы выкинуть GPS, серийники и т.д.
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        var bytes = await EncodeAsync(image, actualContentType, ct);
        activity?.SetTag("image.output_size_bytes", bytes.Length);

        return new ProcessedImage(
            Bytes: bytes,
            ContentType: actualContentType,
            Extension: normalizedExtension);
    }

    private static async Task<byte[]> EncodeAsync(Image image, string contentType, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        // Re-encode в исходном формате — никакого silent-конвертирования.
        // EXIF/IPTC/XMP уже сброшены в Metadata.
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
            default:
                // unreachable — guard'или выше
                throw new InvalidOperationException($"Unsupported content type {contentType}");
        }
        return ms.ToArray();
    }

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
