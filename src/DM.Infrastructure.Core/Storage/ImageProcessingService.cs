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
using ImageMagick;
using ImageMagick.Formats;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class ImageProcessingService : IImageProcessingService
{
    /// <summary>Maximum source size on any side (decompression-bomb protection after decode).</summary>
    public const int MaxDimension = 8192;
    /// <summary>
    /// Maximum decoded area in pixels, against a decompression bomb: the square of
    /// MaxDimension.
    /// </summary>
    public const long MaxDecodedPixels = 64L * 1024 * 1024;

    /// <summary>
    /// Most frames an animation may carry: ImageMagick spends memory on every frame
    /// before a pixel, which the pixel budget does not see.
    /// </summary>
    public const int MaxFrames = 1000;

    /// <summary>
    /// Ceiling of ImageMagick's pixel cache, shared by every picture being decoded:
    /// the largest picture the budget admits, at four bytes a pixel.
    /// </summary>
    public const ulong MaxPixelCacheBytes = MaxDecodedPixels * 4;

    private const string Unreadable = "Не удалось прочитать изображение";
    private const string TooManyPixels = "Изображение содержит слишком много пикселей";

    /// <summary>
    /// How a format is decoded, stored and measured.
    /// </summary>
    /// <param name="Format">The decoder ImageMagick is told to use.</param>
    /// <param name="Extension">The extension a stored key gets.</param>
    /// <param name="HasCanvas">
    /// The picture is the canvas its frames are laid on rather than its first
    /// frame: a GIF or a WebP animation may open with a frame smaller than itself.
    /// A PNG may carry a page too (oFFs, vpAg), which browsers ignore.
    /// </param>
    /// <param name="Name">The format as a refusal names it.</param>
    private sealed record Codec(MagickFormat Format, string Extension, bool HasCanvas, string Name);

    private static readonly Dictionary<string, Codec> CodecByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = new(MagickFormat.Jpeg, ".jpg", HasCanvas: false, "JPEG"),
        ["image/png"] = new(MagickFormat.Png, ".png", HasCanvas: false, "PNG"),
        ["image/webp"] = new(MagickFormat.WebP, ".webp", HasCanvas: true, "WebP"),
        ["image/gif"] = new(MagickFormat.Gif, ".gif", HasCanvas: true, "GIF"),
    };

    /// <summary>
    /// PNG chunks the encoder leaves out: text, EXIF, times, the page, and the
    /// gamma and background hints. The color profile (iCCP, sRGB) stays.
    /// </summary>
    /// <remarks>
    /// zTXt is not on the list although it is text: with both tEXt and zTXt left
    /// out ImageMagick writes no profile at all, the color profile included. What
    /// could reach a zTXt is taken off the frame beforehand instead.
    /// </remarks>
    private const string PngChunksLeftOut = "bKGD,caNv,cHRM,eXIf,gAMA,iTXt,oFFs,pHYs,tEXt,tIME,vpAg,date";

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

    private static readonly TypeRules AvatarRules = new(
        ImageProcessingDefaults.AvatarContentTypes,
        ImageProcessingDefaults.AvatarMinDimension,
        ImageProcessingDefaults.OriginalMaxDimension);

    /// <summary>
    /// Per-type rules. An avatar is shown at a fixed small size, so it is floored
    /// and downscaled; an attachment is a map or a scheme whose detail is the
    /// point, so it keeps its own dimensions and is only re-encoded.
    /// </summary>
    private static readonly Dictionary<UploadType, TypeRules> RulesByType = new()
    {
        [UploadType.UserAvatar] = AvatarRules,
        [UploadType.CharacterAvatar] = AvatarRules,
        [UploadType.PostAttachment] = new TypeRules(
            ImageProcessingDefaults.PostAttachmentContentTypes,
            MinDimension: 0,
            MaxStoredDimension: null),
    };

    /// <summary>
    /// ImageMagick's limits belong to the process. By default the cache takes half
    /// the memory of the host, not the container, and spills to disk.
    /// </summary>
    static ImageProcessingService()
    {
        ResourceLimits.Memory = MaxPixelCacheBytes;
        ResourceLimits.Disk = 0;
        ResourceLimits.ListLength = MaxFrames;
    }

    /// <inheritdoc />
    public async Task<ProcessedImage> ProcessAsync(
        Stream input,
        string declaredContentType,
        UploadType type,
        CancellationToken ct = default)
    {
        if (!RulesByType.TryGetValue(type, out var rules))
        {
            // Fails closed: a type without rules refuses rather than borrow another set.
            throw new InvalidOperationException($"No image processing rules for {type}");
        }

        using var activity = DmActivitySource.Source.StartActivity(
            "image.process",
            ActivityKind.Internal);
        activity?.SetTag("image.declared_content_type", declaredContentType);
        activity?.SetTag("image.upload_type", type.ToString());

        // The format is read from the bytes, never from the declared type.
        var buffered = await BufferStreamAsync(input, ct);
        activity?.SetTag("image.input_size_bytes", buffered.Length);

        var actualContentType = ContentTypeOf(buffered) ?? throw Refusal("Файл не является изображением");
        if (!rules.ContentTypes.Contains(actualContentType) ||
            !CodecByContentType.TryGetValue(actualContentType, out var codec))
        {
            throw Refusal($"Допустимые форматы: {FormatList(rules.ContentTypes)}");
        }

        // The decoder is named, so ImageMagick refuses bytes that are not in the format.
        var settings = new MagickReadSettings { Format = codec.Format };

        Identify(buffered, settings, codec, rules);
        ct.ThrowIfCancellationRequested();

        using var frames = Decode(buffered, settings);

        // An attachment keeps its size but is re-encoded, which strips its metadata
        // and makes the stored bytes a file this decoder produced.
        Downscale(frames, codec, rules.MaxStoredDimension);
        foreach (var frame in frames)
        {
            ConvertToSrgb(frame);
            StripMetadata(frame);
        }

        var bytes = Encode(frames, actualContentType);
        var (width, height) = CanvasOf(frames[0], codec);
        var thumbnails = AvatarThumbnail.SizesFor(type)
            .Select(size => new ProcessedThumbnail(size, SquareThumbnail(frames[0], (uint)size)))
            .ToArray();
        activity?.SetTag("image.output_size_bytes", bytes.Length);
        activity?.SetTag("image.output_width", width);
        activity?.SetTag("image.output_height", height);

        return new ProcessedImage(
            Bytes: bytes,
            ContentType: actualContentType,
            Extension: codec.Extension,
            Width: (int)width,
            Height: (int)height)
        {
            Thumbnails = thumbnails,
        };
    }

    /// <summary>
    /// The format the bytes are in, by their signature, or null when they carry
    /// none this pipeline knows.
    /// </summary>
    /// <remarks>
    /// TIFF and BMP are known so that the refusal names the accepted formats.
    /// </remarks>
    private static string? ContentTypeOf(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }
        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }
        if (bytes.StartsWith("II*\0"u8) || bytes.StartsWith("MM\0*"u8))
        {
            return "image/tiff";
        }
        return bytes.StartsWith("BM"u8) ? "image/bmp" : null;
    }

    private static void Identify(byte[] bytes, MagickReadSettings settings, Codec codec, TypeRules rules)
    {
        uint width, height;
        int frameCount;
        try
        {
            using var pinged = new MagickImageCollection();
            pinged.Ping(bytes, settings);
            if (pinged.Count == 0)
            {
                throw Refusal(Unreadable);
            }
            (width, height) = CanvasOf(pinged[0], codec);
            frameCount = pinged.Count;
        }
        catch (MagickResourceLimitErrorException)
        {
            // More frames than MaxFrames.
            throw Refusal(TooManyPixels);
        }
        catch (MagickException)
        {
            throw Refusal(Unreadable);
        }

        if (rules.MinDimension > 0 && (width < rules.MinDimension || height < rules.MinDimension))
        {
            throw Refusal($"Изображение слишком маленькое (минимум {rules.MinDimension}x{rules.MinDimension})");
        }

        if (width > MaxDimension || height > MaxDimension)
        {
            throw Refusal($"Изображение слишком большое (максимум {MaxDimension}x{MaxDimension})");
        }

        // Every frame of an animation is decoded into memory.
        if ((long)width * height * frameCount > MaxDecodedPixels)
        {
            throw Refusal(TooManyPixels);
        }
    }

    private static MagickImageCollection Decode(byte[] bytes, MagickReadSettings settings)
    {
        try
        {
            return new MagickImageCollection(bytes, settings);
        }
        catch (Exception e) when (e is MagickResourceLimitErrorException or MagickCacheErrorException)
        {
            // Past MaxPixelCacheBytes, alone or with the pictures decoded at the same moment.
            throw Refusal(TooManyPixels);
        }
        catch (MagickException)
        {
            // Headers that read and a body that does not.
            throw Refusal(Unreadable);
        }
    }

    private static void Downscale(MagickImageCollection frames, Codec codec, int? maxStoredDimension)
    {
        if (maxStoredDimension is not { } max)
        {
            return;
        }

        var (width, height) = CanvasOf(frames[0], codec);
        if (width <= max && height <= max)
        {
            return;
        }

        // An animation is laid out on its canvas first: a frame smaller than the
        // canvas, scaled on its own, would keep the offset of the old size.
        if (codec.HasCanvas)
        {
            frames.Coalesce();
        }

        // Fits the box and keeps the aspect ratio.
        var box = new MagickGeometry((uint)max, (uint)max);
        foreach (var frame in frames)
        {
            frame.Resize(box);
            frame.ResetPage();
        }
    }

    /// <summary>
    /// The stored picture is in sRGB, or in gray, which every browser draws the
    /// same way.
    /// </summary>
    /// <remarks>
    /// For a CMYK JPEG, which browsers show differently, and a linear PNG, whose
    /// gamma chunk is left out on writing.
    /// </remarks>
    private static void ConvertToSrgb(IMagickImage<byte> frame)
    {
        if (frame.ColorSpace is ColorSpace.sRGB or ColorSpace.Gray)
        {
            return;
        }

        // Through the picture's own profile where it carries one, by formula
        // otherwise: a CMYK profile is what says which ink a value meant.
        var throughProfile = frame.GetColorProfile() is not null && frame.TransformColorSpace(ColorProfiles.SRGB);
        if (!throughProfile)
        {
            frame.ColorSpace = ColorSpace.sRGB;
        }

        // The profile described the bytes before the conversion, and sRGB is what
        // a picture without one is taken to be.
        frame.RemoveProfile("icc");
    }

    /// <summary>
    /// Every profile but the color one goes, and every attribute but the "jpeg:"
    /// notes, so GPS coordinates, serials and authors do not reach the stored file.
    /// </summary>
    /// <remarks>
    /// Without the color profile a wide-gamut picture looks washed out; without the
    /// "jpeg:" notes a 4:2:0 JPEG is stored at 4:4:4. Attributes go because the PNG
    /// encoder writes them out as text.
    /// </remarks>
    private static void StripMetadata(IMagickImage<byte> frame)
    {
        foreach (var name in frame.ProfileNames.ToArray())
        {
            if (name != "icc")
            {
                frame.RemoveProfile(name);
            }
        }
        foreach (var name in frame.AttributeNames.ToArray())
        {
            if (!name.StartsWith("jpeg:", StringComparison.Ordinal))
            {
                frame.RemoveAttribute(name);
            }
        }
    }

    private static byte[] Encode(MagickImageCollection frames, string contentType)
    {
        switch (contentType)
        {
            case "image/jpeg":
                foreach (var frame in frames)
                {
                    frame.Quality = 90;
                }
                return frames.ToByteArray(MagickFormat.Jpeg);
            case "image/png":
                foreach (var frame in frames)
                {
                    frame.SetArtifact("png:exclude-chunk", PngChunksLeftOut);
                }
                return frames.ToByteArray(new PngWriteDefines { CompressionLevel = 9 });
            case "image/webp":
                foreach (var frame in frames)
                {
                    frame.Quality = 90;
                }
                return frames.ToByteArray(new WebPWriteDefines { Lossless = false });
            case "image/gif":
                return frames.ToByteArray(MagickFormat.Gif);
            default:
                throw new InvalidOperationException($"Unsupported content type {contentType}");
        }
    }

    /// <remarks>
    /// Cut from the frame already stripped of its metadata. ImageMagick reads every
    /// frame of a WebP animation onto the whole canvas, so the first one needs no
    /// laying out.
    /// </remarks>
    private static byte[] SquareThumbnail(IMagickImage<byte> first, uint side)
    {
        using var square = first.Clone();
        square.Resize(new MagickGeometry(side, side) { FillArea = true });
        square.Crop(side, side, Gravity.Center);
        square.ResetPage();
        square.Quality = 85;
        return square.ToByteArray(new WebPWriteDefines { Lossless = false });
    }

    /// <summary>
    /// The size of the picture as a browser lays it out.
    /// </summary>
    private static (uint Width, uint Height) CanvasOf(IMagickImage first, Codec codec) => codec.HasCanvas
        ? (Math.Max(first.Width, first.Page.Width), Math.Max(first.Height, first.Page.Height))
        : (first.Width, first.Height);

    private static HttpBadRequestException Refusal(string message) =>
        new(new Dictionary<string, string> { ["file"] = message });

    private static string FormatList(IReadOnlyCollection<string> contentTypes) =>
        string.Join(", ", contentTypes.Select(type => CodecByContentType[type].Name));

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
