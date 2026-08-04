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
/// Unit tests для <see cref="ImageProcessingService"/> — magic-byte валидация,
/// decompression-bomb защита, EXIF-strip, нормализация расширения, single
/// source-file output. Thumbnails больше не пре-генерируются — imgproxy
/// делает это on-the-fly при serving.
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

        var result = await _sut.ProcessAsync(input, "image/jpeg");

        result.Bytes.Should().NotBeEmpty();
        result.ContentType.Should().Be("image/jpeg");
        result.Extension.Should().Be(".jpg");
    }

    [Fact]
    public async Task ProcessAsync_AcceptsValidPng_PreservesPngFormat()
    {
        var input = CreatePngStream(width: 600, height: 400);

        var result = await _sut.ProcessAsync(input, "image/png");

        result.ContentType.Should().Be("image/png");
        result.Extension.Should().Be(".png");
    }

    [Fact]
    public async Task ProcessAsync_AcceptsValidWebp_PreservesWebpFormat()
    {
        var input = CreateWebpStream(width: 500, height: 500);

        var result = await _sut.ProcessAsync(input, "image/webp");

        result.ContentType.Should().Be("image/webp");
        result.Extension.Should().Be(".webp");
    }

    [Fact]
    public async Task ProcessAsync_DownscalesLargeImages_To1024Max()
    {
        var input = CreateJpegStream(width: 2000, height: 1500);

        var result = await _sut.ProcessAsync(input, "image/jpeg");

        using var image = Image.Load(result.Bytes);
        Math.Max(image.Width, image.Height)
            .Should().BeLessOrEqualTo(ImageProcessingService.OriginalMaxDimension);
    }

    [Fact]
    public async Task ProcessAsync_PreservesAspectRatio_OnDownscale()
    {
        // 2:1 aspect ratio — нужно сохранить.
        var input = CreateJpegStream(width: 2000, height: 1000);

        var result = await _sut.ProcessAsync(input, "image/jpeg");

        using var image = Image.Load(result.Bytes);
        var ratio = (double)image.Width / image.Height;
        ratio.Should().BeApproximately(2.0, 0.01);
    }

    [Fact]
    public async Task ProcessAsync_KeepsSmallImagesUnchanged()
    {
        var input = CreateJpegStream(width: 200, height: 200);

        var result = await _sut.ProcessAsync(input, "image/jpeg");

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

        var result = await _sut.ProcessAsync(input, "image/jpeg");

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

        var result = await _sut.ProcessAsync(input, "image/jpeg");

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

        var act = () => _sut.ProcessAsync(input, "image/jpeg");

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonImageContent()
    {
        var notAnImage = new MemoryStream(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }); // "Hello"

        var act = () => _sut.ProcessAsync(notAnImage, "image/jpeg");

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsHtmlMasqueradingAsImage()
    {
        // Client lies about content-type — magic-byte detection должна это поймать.
        var htmlBytes = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>oops</body></html>");
        var stream = new MemoryStream(htmlBytes);

        var act = () => _sut.ProcessAsync(stream, "image/png");

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsGif_NotInWhitelist()
    {
        // Mini-GIF (1×1 transparent). Заголовок GIF89a → ImageSharp его распознает,
        // но наш whitelist не пускает.
        var gifBytes = new byte[]
        {
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, // GIF89a
            0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
            0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
            0x21, 0xF9, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0x02, 0x02, 0x4C, 0x01, 0x00, 0x3B,
        };
        var stream = new MemoryStream(gifBytes);

        var act = () => _sut.ProcessAsync(stream, "image/gif");

        await act.Should().ThrowAsync<HttpBadRequestException>();
    }

    [Fact]
    public async Task ProcessAsync_StripsExifMetadata()
    {
        // Создаем JPEG с EXIF; после processing EXIF должен исчезнуть.
        using var image = new Image<Rgba32>(800, 600);
        image.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
        image.Metadata.ExifProfile.SetValue(
            SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Make,
            "TestCamera");

        using var inputStream = new MemoryStream();
        await image.SaveAsJpegAsync(inputStream);
        inputStream.Position = 0;

        var result = await _sut.ProcessAsync(inputStream, "image/jpeg");

        using var processed = Image.Load(result.Bytes);
        processed.Metadata.ExifProfile.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_NormalizesExtensionFromContentType_NotFromFileName()
    {
        // Файл реально PNG → возвращаем .png независимо от того, что юзер
        // прислал в filename. Anti-extension-spoofing.
        var input = CreatePngStream(width: 400, height: 400);

        var result = await _sut.ProcessAsync(input, "image/png");

        result.Extension.Should().Be(".png");
    }

    // --- Helpers: создание тестовых изображений ---

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
}
