using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using AwesomeAssertions;
using ImageMagick;
using ImageMagick.Formats;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Storage;

public class ImageProcessingServiceShould
{
    private readonly ImageProcessingService _sut = new();

    // A type added to the enum without rules still fails closed, but as a server error on every
    // file it is sent; this is where it fails first.
    [Fact]
    public async Task ProcessAsyncHasRulesForEveryUploadType()
    {
        foreach (var type in Enum.GetValues<UploadType>())
        {
            var notAnImage = new MemoryStream(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }); // "Hello"

            var act = () => _sut.ProcessAsync(notAnImage, "image/png", type);

            await act.Should().ThrowAsync<HttpBadRequestException>(
                "{0} has to be refused for what the file is, not for having no rules", type);
        }
    }

    [Fact]
    public async Task ProcessAsyncAcceptsValidJpeg()
    {
        var input = CreateJpegStream(width: 800, height: 600);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        result.Bytes.Should().NotBeEmpty();
        result.ContentType.Should().Be("image/jpeg");
        result.Extension.Should().Be(".jpg");
    }

    [Fact]
    public async Task ProcessAsyncAcceptsValidPngPreservesPngFormat()
    {
        var input = CreatePngStream(width: 600, height: 400);

        var result = await _sut.ProcessAsync(input, "image/png", UploadType.UserAvatar);

        result.ContentType.Should().Be("image/png");
        result.Extension.Should().Be(".png");
    }

    [Fact]
    public async Task ProcessAsyncAcceptsValidWebpPreservesWebpFormat()
    {
        var input = CreateWebpStream(width: 500, height: 500);

        var result = await _sut.ProcessAsync(input, "image/webp", UploadType.UserAvatar);

        result.ContentType.Should().Be("image/webp");
        result.Extension.Should().Be(".webp");
    }

    [Fact]
    public async Task ProcessAsyncDownscalesLargeImagesTo1024Max()
    {
        var input = CreateJpegStream(width: 2000, height: 1500);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = new MagickImage(result.Bytes);
        Math.Max(image.Width, image.Height)
            .Should().BeLessThanOrEqualTo(ImageProcessingDefaults.OriginalMaxDimension);
    }

    [Fact]
    public async Task ProcessAsyncPreservesAspectRatioOnDownscale()
    {
        // 2:1 aspect ratio — it has to survive the downscale.
        var input = CreateJpegStream(width: 2000, height: 1000);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = new MagickImage(result.Bytes);
        var ratio = (double)image.Width / image.Height;
        ratio.Should().BeApproximately(2.0, 0.01);
    }

    [Fact]
    public async Task ProcessAsyncKeepsSmallImagesUnchanged()
    {
        var input = CreateJpegStream(width: 200, height: 200);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = new MagickImage(result.Bytes);
        image.Width.Should().Be(200);
        image.Height.Should().Be(200);
    }

    // The result carries the dimensions of the bytes it carries, so a page reserves the exact box.
    [Fact]
    public async Task ProcessAsyncReportsTheDimensionsOfTheStoredBytes()
    {
        var input = CreateJpegStream(width: 200, height: 150);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = new MagickImage(result.Bytes);
        result.Width.Should().Be((int)image.Width);
        result.Height.Should().Be((int)image.Height);
        result.Width.Should().Be(200);
        result.Height.Should().Be(150);
    }

    [Fact]
    public async Task ProcessAsyncReportsTheDownscaledDimensionsNotTheSubmittedOnes()
    {
        var input = CreateJpegStream(width: 2000, height: 1000);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = new MagickImage(result.Bytes);
        result.Width.Should().Be((int)image.Width).And
            .Be(ImageProcessingDefaults.OriginalMaxDimension);
        result.Height.Should().Be((int)image.Height).And
            .Be(ImageProcessingDefaults.OriginalMaxDimension / 2);
    }

    [Fact]
    public async Task ProcessAsyncRejectsTooSmallImage()
    {
        var input = CreateJpegStream(
            width: ImageProcessingDefaults.AvatarMinDimension - 1,
            height: ImageProcessingDefaults.AvatarMinDimension - 1);

        var act = () => _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsyncRejectsNonImageContent()
    {
        var notAnImage = new MemoryStream(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }); // "Hello"

        var act = () => _sut.ProcessAsync(notAnImage, "image/jpeg", UploadType.UserAvatar);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsyncRejectsHtmlMasqueradingAsImage()
    {
        // Client lies about content-type — magic-byte detection has to catch it.
        var htmlBytes = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>oops</body></html>");
        var stream = new MemoryStream(htmlBytes);

        var act = () => _sut.ProcessAsync(stream, "image/png", UploadType.UserAvatar);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsyncRejectsGifOnAnAvatarNotInThatWhitelist()
    {
        // The GIF89a header → the signature names it, but the avatar allow-list
        // does not carry it. An attachment's does; see the pair below.
        var stream = CreateGifStream(width: 64, height: 64);

        var act = () => _sut.ProcessAsync(stream, "image/gif", UploadType.UserAvatar);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    // --- Post attachments: their own formats, no floor, no downscale ---

    /// <summary>
    /// An attachment keeps its size: a scanned map downscaled to 1024 px stops being readable.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncKeepsAPostAttachmentAtItsOwnSize()
    {
        var oversized = ImageProcessingDefaults.OriginalMaxDimension * 3;
        var input = CreateJpegStream(width: oversized, height: oversized / 2);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.PostAttachment);

        result.Width.Should().Be(oversized);
        result.Height.Should().Be(oversized / 2);
    }

    // The same picture as an avatar, to show that the difference is the type and not the picture.
    [Fact]
    public async Task ProcessAsyncStillDownscalesAnAvatarOfTheSameSize()
    {
        var oversized = ImageProcessingDefaults.OriginalMaxDimension * 3;
        var input = CreateJpegStream(width: oversized, height: oversized / 2);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        result.Width.Should().Be(ImageProcessingDefaults.OriginalMaxDimension);
    }

    [Fact]
    public async Task ProcessAsyncAcceptsASmallPostAttachmentWithNoDimensionFloor()
    {
        var input = CreatePngStream(
            width: ImageProcessingDefaults.AvatarMinDimension - 1,
            height: ImageProcessingDefaults.AvatarMinDimension - 1);

        var result = await _sut.ProcessAsync(input, "image/png", UploadType.PostAttachment);

        result.Width.Should().Be(ImageProcessingDefaults.AvatarMinDimension - 1);
    }

    [Fact]
    public async Task ProcessAsyncAcceptsGifOnAPostAttachment()
    {
        var stream = CreateGifStream(width: 32, height: 32);

        var result = await _sut.ProcessAsync(stream, "image/gif", UploadType.PostAttachment);

        result.ContentType.Should().Be("image/gif");
        result.Extension.Should().Be(".gif");
    }

    [Fact]
    public async Task ProcessAsyncKeepsEveryFrameOfAnAnimatedGif()
    {
        var stream = CreateGifStream(width: 32, height: 32, frames: 3);

        var result = await _sut.ProcessAsync(stream, "image/gif", UploadType.PostAttachment);

        using var stored = new MagickImageCollection(result.Bytes);
        stored.Count.Should().Be(3);
    }

    // The pixel budget is the canvas times the frames: one-pixel frames on a quarter-budget canvas
    // go over by count, refused before decoding.
    [Fact]
    public async Task ProcessAsyncCountsEveryFrameOfAnAnimationAgainstThePixelBudget()
    {
        const int canvas = 4096;
        var frames = (int)(ImageProcessingService.MaxDecodedPixels / ((long)canvas * canvas)) + 1;
        var stream = CreateGifStream(width: 1, height: 1, frames, canvas);

        var act = () => _sut.ProcessAsync(stream, "image/gif", UploadType.PostAttachment);

        (await act.Should().ThrowAsync<HttpBadRequestException>())
            .Which.ValidationErrors["file"].Should().Be("Изображение содержит слишком много пикселей");
    }

    /// <summary>
    /// The formats refused, each by the signature a caller would send: by what the bytes are, not what the
    /// request calls them.
    /// </summary>
    [Theory]
    [InlineData("%PDF-1.7\n1 0 obj", "application/pdf", "document.pdf")]
    [InlineData("Just some notes.", "text/plain", "notes.txt")]
    public async Task ProcessAsyncRejectsDocumentsAsPostAttachments(
        string content, string declaredContentType, string _)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var act = () => _sut.ProcessAsync(stream, declaredContentType, UploadType.PostAttachment);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsyncRejectsAZipContainerCallingItselfAJpeg()
    {
        // "PK\x03\x04" — the local file header every zip (and every docx) starts with.
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00 };
        var stream = new MemoryStream(zipBytes);

        var act = () => _sut.ProcessAsync(stream, "image/jpeg", UploadType.PostAttachment);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    /// <summary>
    /// A TIFF sent as a JPEG: the decoder reads it, so the signature refuses it first and names what is accepted.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncRejectsATiffCallingItselfAJpegByItsSignature()
    {
        using var image = new MagickImage(new MagickColor(90, 90, 90), 120, 90);
        var stream = new MemoryStream(image.ToByteArray(MagickFormat.Tiff));

        var act = () => _sut.ProcessAsync(stream, "image/jpeg", UploadType.PostAttachment);

        (await act.Should().ThrowAsync<HttpBadRequestException>())
            .Which.ValidationErrors["file"].Should().StartWith("Допустимые форматы");
    }

    [Fact]
    public async Task ProcessAsyncNamesTheAvatarFormatsInTheRefusal()
    {
        var act = () => _sut.ProcessAsync(CreateGifStream(width: 64, height: 64), "image/gif", UploadType.UserAvatar);

        (await act.Should().ThrowAsync<HttpBadRequestException>())
            .Which.ValidationErrors["file"].Should().Be("Допустимые форматы: JPEG, PNG, WebP");
    }

    [Fact]
    public async Task ProcessAsyncNamesTheAttachmentFormatsInTheRefusal()
    {
        using var image = new MagickImage(new MagickColor(90, 90, 90), 120, 90);
        var stream = new MemoryStream(image.ToByteArray(MagickFormat.Tiff));

        var act = () => _sut.ProcessAsync(stream, "image/jpeg", UploadType.PostAttachment);

        (await act.Should().ThrowAsync<HttpBadRequestException>())
            .Which.ValidationErrors["file"].Should().Be("Допустимые форматы: JPEG, PNG, WebP, GIF");
    }

    [Fact]
    public async Task ProcessAsyncNormalizesAPostAttachmentExtensionFromItsBytes()
    {
        var input = CreatePngStream(width: 120, height: 90);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.PostAttachment);

        result.ContentType.Should().Be("image/png");
        result.Extension.Should().Be(".png");
    }

    [Fact]
    public async Task ProcessAsyncStripsExifMetadata()
    {
        // Build a JPEG carrying EXIF; after processing the EXIF must be gone.
        using var image = new MagickImage(new MagickColor(128, 128, 128), 800, 600);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "TestCamera");
        image.SetProfile(exif);
        var inputStream = new MemoryStream(image.ToByteArray(MagickFormat.Jpeg));

        var result = await _sut.ProcessAsync(inputStream, "image/jpeg", UploadType.UserAvatar);

        using var processed = new MagickImage(result.Bytes);
        processed.GetExifProfile().Should().BeNull();
    }

    // A CMYK JPEG is stored in sRGB: browsers disagree on CMYK.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessAsyncStoresACmykJpegInSrgb(bool withProfile)
    {
        var red = new MagickColor(200, 40, 40);
        using var image = new MagickImage(red, 64, 64);
        if (withProfile)
        {
            image.TransformColorSpace(ColorProfiles.SRGB, ColorProfiles.USWebCoatedSWOP);
        }
        else
        {
            image.ColorSpace = ColorSpace.CMYK;
        }
        var input = new MemoryStream(image.ToByteArray(MagickFormat.Jpeg));

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.PostAttachment);

        using var stored = new MagickImage(result.Bytes);
        stored.ColorSpace.Should().Be(ColorSpace.sRGB);
        stored.GetColorProfile().Should().BeNull();
        var pixel = stored.GetPixels().GetPixel(32, 32).ToColor()!;
        new[] { pixel.R - red.R, pixel.G - red.G, pixel.B - red.B }
            .Should().OnlyContain(difference => Math.Abs(difference) <= 16);
    }

    [Fact]
    public async Task ProcessAsyncNormalizesExtensionFromContentTypeNotFromFileName()
    {
        // The file really is a PNG → return .png whatever the user sent in the
        // filename. Anti-extension-spoofing.
        var input = CreatePngStream(width: 400, height: 400);

        var result = await _sut.ProcessAsync(input, "image/png", UploadType.UserAvatar);

        result.Extension.Should().Be(".png");
    }

    // --- Avatar thumbnails: squares cut at upload, stored beside the original ---

    [Theory]
    [InlineData(UploadType.UserAvatar)]
    [InlineData(UploadType.CharacterAvatar)]
    public async Task ProcessAsyncCutsAWebpSquareOfEverySideOfAnAvatar(UploadType type)
    {
        var input = CreateJpegStream(width: 800, height: 300);

        var result = await _sut.ProcessAsync(input, "image/jpeg", type);

        result.Thumbnails.Select(thumbnail => thumbnail.Size)
            .Should().Equal(AvatarThumbnail.SmallSize, AvatarThumbnail.MediumSize);
        foreach (var thumbnail in result.Thumbnails)
        {
            using var square = new MagickImage(thumbnail.Bytes);
            square.Format.Should().Be(MagickFormat.WebP, "the API names every thumbnail {0}", AvatarThumbnail.ContentType);
            square.Width.Should().Be((uint)thumbnail.Size);
            square.Height.Should().Be((uint)thumbnail.Size);
        }
    }

    [Fact]
    public async Task ProcessAsyncCutsTheThumbnailFromTheCenterOfThePicture()
    {
        // Three upright thirds, and the square of a 3:1 picture is exactly the middle one.
        var middle = MagickColors.Lime;
        using var columns = new MagickImageCollection
        {
            new MagickImage(MagickColors.Red, 100, 100),
            new MagickImage(middle, 100, 100),
            new MagickImage(MagickColors.Blue, 100, 100),
        };
        using var thirds = columns.AppendHorizontally();
        var input = new MemoryStream(thirds.ToByteArray(MagickFormat.Png));

        var result = await _sut.ProcessAsync(input, "image/png", UploadType.UserAvatar);

        result.Thumbnails.Should().NotBeEmpty();
        foreach (var thumbnail in result.Thumbnails)
        {
            using var square = new MagickImage(thumbnail.Bytes);
            var inset = thumbnail.Size / 10;
            foreach (var x in new[] { inset, thumbnail.Size - inset })
            {
                var pixel = square.GetPixels().GetPixel(x, thumbnail.Size / 2).ToColor()!;
                new[] { pixel.R - middle.R, pixel.G - middle.G, pixel.B - middle.B }
                    .Should().OnlyContain(difference => Math.Abs(difference) <= 16,
                        "the {0} square shows the middle third at x {1}", thumbnail.Size, x);
            }
        }
    }

    // The thumbnails are cut from the cleaned picture, so they carry no more of the camera than it
    // does.
    [Fact]
    public async Task ProcessAsyncStripsExifFromTheThumbnails()
    {
        using var image = new MagickImage(new MagickColor(128, 128, 128), 800, 600);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "TestCamera");
        image.SetProfile(exif);
        var input = new MemoryStream(image.ToByteArray(MagickFormat.Jpeg));

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        result.Thumbnails.Should().NotBeEmpty();
        foreach (var thumbnail in result.Thumbnails)
        {
            using var square = new MagickImage(thumbnail.Bytes);
            square.GetExifProfile().Should().BeNull();
        }
    }

    // The encoder stores the first frame below as the red quarter alone at an offset, and the
    // reader lays it back on the canvas: stretched over the square instead, the quarter would fill
    // it.
    [Fact]
    public async Task ProcessAsyncCutsAStillThumbnailOfTheFirstFrameOfAnAnimationOnItsCanvas()
    {
        using var animation = new MagickImageCollection();
        var first = new MagickImage(MagickColors.Transparent, 200, 200) { AnimationDelay = 10 };
        using (var quarter = new MagickImage(MagickColors.Red, 100, 100))
        {
            first.Composite(quarter, 100, 100, CompositeOperator.Over);
        }
        animation.Add(first);
        animation.Add(new MagickImage(MagickColors.Blue, 200, 200) { AnimationDelay = 10 });
        var input = new MemoryStream(animation.ToByteArray(new WebPWriteDefines { Lossless = true }));

        var result = await _sut.ProcessAsync(input, "image/webp", UploadType.UserAvatar);

        using var square = new MagickImageCollection(result.Thumbnails[0].Bytes);
        square.Should().ContainSingle("a thumbnail is a still");
        var pixels = square[0].GetPixels();
        pixels.GetPixel(20, 20).ToColor()!.R.Should().BeLessThan(64, "the rest of the canvas is not the quarter");
        pixels.GetPixel(80, 80).ToColor()!.R.Should().BeGreaterThan(200, "and the red quarter stays in its corner");
    }

    // An attachment is served by the endpoint that authorizes the caller, and a thumbnail would
    // have none.
    [Fact]
    public async Task ProcessAsyncCutsNoThumbnailOfAPostAttachment()
    {
        var input = CreateJpegStream(width: 800, height: 600);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.PostAttachment);

        result.Thumbnails.Should().BeEmpty();
    }

    // --- Helpers: building test images ---

    private static MemoryStream CreateJpegStream(int width, int height) =>
        Encode(width, height, new MagickColor(128, 128, 128), MagickFormat.Jpeg);

    private static MemoryStream CreatePngStream(int width, int height) =>
        Encode(width, height, new MagickColor(100, 100, 100), MagickFormat.Png);

    private static MemoryStream CreateWebpStream(int width, int height) =>
        Encode(width, height, new MagickColor(80, 80, 80), MagickFormat.WebP);

    private static MemoryStream Encode(int width, int height, MagickColor color, MagickFormat format)
    {
        using var image = new MagickImage(color, (uint)width, (uint)height) { Quality = 80 };
        return new MemoryStream(image.ToByteArray(format));
    }

    /// <param name="frames">How many frames, one for a still picture.</param>
    /// <param name="canvas">Side of the canvas the frames are laid on, 0 for the frame's own size.</param>
    private static MemoryStream CreateGifStream(int width, int height, int frames = 1, int canvas = 0)
    {
        using var animation = new MagickImageCollection();
        for (var i = 0; i < frames; i++)
        {
            // A different shade per frame, so no encoder folds two of them together.
            var frame = new MagickImage(new MagickColor((byte)(60 + i * 10), 60, 60), (uint)width, (uint)height)
            {
                AnimationDelay = 10,
            };
            if (canvas > 0)
            {
                frame.Page = new MagickGeometry(0, 0, (uint)canvas, (uint)canvas);
            }
            animation.Add(frame);
        }
        return new MemoryStream(animation.ToByteArray(MagickFormat.Gif));
    }
}
