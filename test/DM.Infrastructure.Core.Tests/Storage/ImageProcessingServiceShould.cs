using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Image = SixLabors.ImageSharp.Image;

namespace DM.Infrastructure.Core.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="ImageProcessingService"/> — magic-byte validation,
/// decompression-bomb protection, EXIF strip, extension normalisation, single
/// source-file output. Thumbnails are no longer pre-generated — imgproxy does
/// that on the fly when serving.
/// </summary>
public class ImageProcessingServiceShould
{
    private readonly ImageProcessingService _sut = new();

    /// <summary>
    /// Every upload type goes through the validating pipeline. A type answering
    /// false here is routed around magic-byte detection and the format allow-list,
    /// and its object lands in the public bucket with a client-chosen Content-Type.
    /// </summary>
    [Theory]
    [InlineData(UploadType.UserAvatar)]
    [InlineData(UploadType.CharacterAvatar)]
    [InlineData(UploadType.PostAttachment)]
    public void ClassifyEveryUploadTypeAsAnImage(UploadType type)
    {
        _sut.IsImageType(type).Should().BeTrue();
    }

    [Fact]
    public void LeaveNoUploadTypeOutsideTheValidatingPipeline()
    {
        var unvalidated = Enum.GetValues<UploadType>().Where(type => !_sut.IsImageType(type));

        unvalidated.Should().BeEmpty(
            "a new upload type must either pass this pipeline or bring its own validation — " +
            "there is no unvalidated path any more");
    }

    [Fact]
    public async Task ProcessAsync_AcceptsValidJpeg_ReturnsSingleVariant()
    {
        var input = CreateJpegStream(width: 800, height: 600);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        result.Bytes.Should().NotBeEmpty();
        result.ContentType.Should().Be("image/jpeg");
        result.Extension.Should().Be(".jpg");
    }

    [Fact]
    public async Task ProcessAsync_AcceptsValidPng_PreservesPngFormat()
    {
        var input = CreatePngStream(width: 600, height: 400);

        var result = await _sut.ProcessAsync(input, "image/png", UploadType.UserAvatar);

        result.ContentType.Should().Be("image/png");
        result.Extension.Should().Be(".png");
    }

    [Fact]
    public async Task ProcessAsync_AcceptsValidWebp_PreservesWebpFormat()
    {
        var input = CreateWebpStream(width: 500, height: 500);

        var result = await _sut.ProcessAsync(input, "image/webp", UploadType.UserAvatar);

        result.ContentType.Should().Be("image/webp");
        result.Extension.Should().Be(".webp");
    }

    [Fact]
    public async Task ProcessAsync_DownscalesLargeImages_To1024Max()
    {
        var input = CreateJpegStream(width: 2000, height: 1500);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = Image.Load(result.Bytes);
        Math.Max(image.Width, image.Height)
            .Should().BeLessOrEqualTo(ImageProcessingService.OriginalMaxDimension);
    }

    [Fact]
    public async Task ProcessAsync_PreservesAspectRatio_OnDownscale()
    {
        // 2:1 aspect ratio — it has to survive the downscale.
        var input = CreateJpegStream(width: 2000, height: 1000);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = Image.Load(result.Bytes);
        var ratio = (double)image.Width / image.Height;
        ratio.Should().BeApproximately(2.0, 0.01);
    }

    [Fact]
    public async Task ProcessAsync_KeepsSmallImagesUnchanged()
    {
        var input = CreateJpegStream(width: 200, height: 200);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = Image.Load(result.Bytes);
        image.Width.Should().Be(200);
        image.Height.Should().Be(200);
    }

    /// <summary>
    /// The result carries the dimensions of the bytes it carries. A consumer
    /// stores them next to the file and a page reserves the exact box the
    /// picture will occupy; measuring them again later means decoding the file
    /// again, and guessing them means the page reflows on decode.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ReportsTheDimensionsOfTheStoredBytes()
    {
        var input = CreateJpegStream(width: 200, height: 150);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = Image.Load(result.Bytes);
        result.Width.Should().Be(image.Width);
        result.Height.Should().Be(image.Height);
        result.Width.Should().Be(200);
        result.Height.Should().Be(150);
    }

    /// <summary>
    /// After a downscale the reported pair describes the file that gets stored,
    /// not the file that arrived. Reporting the input size would put a number in
    /// the database that no object in the bucket has.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ReportsTheDownscaledDimensions_NotTheSubmittedOnes()
    {
        var input = CreateJpegStream(width: 2000, height: 1000);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        using var image = Image.Load(result.Bytes);
        result.Width.Should().Be(image.Width).And
            .Be(ImageProcessingService.OriginalMaxDimension);
        result.Height.Should().Be(image.Height).And
            .Be(ImageProcessingService.OriginalMaxDimension / 2);
    }

    [Fact]
    public async Task ProcessAsync_RejectsTooSmallImage()
    {
        var input = CreateJpegStream(
            width: ImageProcessingService.MinDimension - 1,
            height: ImageProcessingService.MinDimension - 1);

        var act = () => _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonImageContent()
    {
        var notAnImage = new MemoryStream(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }); // "Hello"

        var act = () => _sut.ProcessAsync(notAnImage, "image/jpeg", UploadType.UserAvatar);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsHtmlMasqueradingAsImage()
    {
        // Client lies about content-type — magic-byte detection has to catch it.
        var htmlBytes = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>oops</body></html>");
        var stream = new MemoryStream(htmlBytes);

        var act = () => _sut.ProcessAsync(stream, "image/png", UploadType.UserAvatar);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsGifOnAnAvatar_NotInThatWhitelist()
    {
        // The GIF89a header → ImageSharp recognises it, but the avatar allow-list
        // does not carry it. An attachment's does; see the pair below.
        var stream = CreateGifStream(width: 64, height: 64);

        var act = () => _sut.ProcessAsync(stream, "image/gif", UploadType.UserAvatar);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    // --- Post attachments: their own formats, no floor, no downscale ---

    /// <summary>
    /// The whole reason an attachment is not put through the avatar rules: a
    /// scanned map downscaled to 1024 px stops being readable, and reading it is
    /// what it was attached for.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_KeepsAPostAttachmentAtItsOwnSize()
    {
        var oversized = ImageProcessingDefaults.OriginalMaxDimension * 3;
        var input = CreateJpegStream(width: oversized, height: oversized / 2);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.PostAttachment);

        result.Width.Should().Be(oversized);
        result.Height.Should().Be(oversized / 2);
    }

    /// <summary>
    /// The same picture as an avatar, to show that the difference is the type and
    /// not the picture.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_StillDownscalesAnAvatarOfTheSameSize()
    {
        var oversized = ImageProcessingDefaults.OriginalMaxDimension * 3;
        var input = CreateJpegStream(width: oversized, height: oversized / 2);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.UserAvatar);

        result.Width.Should().Be(ImageProcessingDefaults.OriginalMaxDimension);
    }

    [Fact]
    public async Task ProcessAsync_AcceptsASmallPostAttachment_WithNoDimensionFloor()
    {
        var input = CreatePngStream(
            width: ImageProcessingDefaults.AvatarMinDimension - 1,
            height: ImageProcessingDefaults.AvatarMinDimension - 1);

        var result = await _sut.ProcessAsync(input, "image/png", UploadType.PostAttachment);

        result.Width.Should().Be(ImageProcessingDefaults.AvatarMinDimension - 1);
    }

    [Fact]
    public async Task ProcessAsync_AcceptsGifOnAPostAttachment()
    {
        var stream = CreateGifStream(width: 32, height: 32);

        var result = await _sut.ProcessAsync(stream, "image/gif", UploadType.PostAttachment);

        result.ContentType.Should().Be("image/gif");
        result.Extension.Should().Be(".gif");
    }

    /// <summary>
    /// The formats the owner ruled out, each named by the signature a caller would
    /// actually send. The point is that they are refused by what the bytes are and
    /// not by what the request calls them.
    /// </summary>
    [Theory]
    [InlineData("%PDF-1.7\n1 0 obj", "application/pdf", "document.pdf")]
    [InlineData("Just some notes.", "text/plain", "notes.txt")]
    public async Task ProcessAsync_RejectsDocumentsAsPostAttachments(
        string content, string declaredContentType, string _)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var act = () => _sut.ProcessAsync(stream, declaredContentType, UploadType.PostAttachment);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    /// <summary>
    /// The case the whole magic-byte pass exists for: a file whose name and
    /// declared type both say JPEG and whose bytes are a ZIP container — which is
    /// what a .docx is.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_RejectsAZipContainerCallingItselfAJpeg()
    {
        // "PK\x03\x04" — the local file header every zip (and every docx) starts with.
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00 };
        var stream = new MemoryStream(zipBytes);

        var act = () => _sut.ProcessAsync(stream, "image/jpeg", UploadType.PostAttachment);

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    /// <summary>
    /// A real PNG renamed and re-declared as a JPEG keeps the extension its bytes
    /// earn, not the one the request claimed.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_NormalizesAPostAttachmentExtensionFromItsBytes()
    {
        var input = CreatePngStream(width: 120, height: 90);

        var result = await _sut.ProcessAsync(input, "image/jpeg", UploadType.PostAttachment);

        result.ContentType.Should().Be("image/png");
        result.Extension.Should().Be(".png");
    }

    [Fact]
    public async Task ProcessAsync_StripsExifMetadata()
    {
        // Build a JPEG carrying EXIF; after processing the EXIF must be gone.
        using var image = new Image<Rgba32>(800, 600);
        image.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
        image.Metadata.ExifProfile.SetValue(
            SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Make,
            "TestCamera");

        using var inputStream = new MemoryStream();
        await image.SaveAsJpegAsync(inputStream);
        inputStream.Position = 0;

        var result = await _sut.ProcessAsync(inputStream, "image/jpeg", UploadType.UserAvatar);

        using var processed = Image.Load(result.Bytes);
        processed.Metadata.ExifProfile.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_NormalizesExtensionFromContentType_NotFromFileName()
    {
        // The file really is a PNG → return .png whatever the user sent in the
        // filename. Anti-extension-spoofing.
        var input = CreatePngStream(width: 400, height: 400);

        var result = await _sut.ProcessAsync(input, "image/png", UploadType.UserAvatar);

        result.Extension.Should().Be(".png");
    }

    // --- Helpers: building test images ---

    private static MemoryStream CreateJpegStream(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(128, 128, 128, 255));
        var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new JpegEncoder { Quality = 80 });
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreatePngStream(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(100, 100, 100, 255));
        var ms = new MemoryStream();
        image.SaveAsPng(ms, new PngEncoder());
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateWebpStream(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(80, 80, 80, 255));
        var ms = new MemoryStream();
        image.SaveAsWebp(ms, new WebpEncoder { Quality = 80 });
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateGifStream(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(60, 60, 60, 255));
        var ms = new MemoryStream();
        image.SaveAsGif(ms, new SixLabors.ImageSharp.Formats.Gif.GifEncoder());
        ms.Position = 0;
        return ms;
    }
}
