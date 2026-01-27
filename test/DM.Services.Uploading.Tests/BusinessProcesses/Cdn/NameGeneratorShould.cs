using System;
using System.Threading.Tasks;
using DM.Services.Core.Extensions;
using DM.Services.Core.Implementation;
using DM.Services.Uploading.BusinessProcesses.Cdn;
using DM.Services.Uploading.Dto;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Services.Uploading.Tests.BusinessProcesses.Cdn;

public class NameGeneratorShould : UnitTestBase
{
    private readonly Mock<IGuidFactory> guidFactory;
    private readonly NameGenerator nameGenerator;

    public NameGeneratorShould()
    {
        guidFactory = Mock<IGuidFactory>();
        nameGenerator = new NameGenerator(guidFactory.Object);
    }

    [Theory]
    [InlineData(FileMimeTypeNames.Image.Jpeg, ".jpg")]
    [InlineData(FileMimeTypeNames.Image.Pjpeg, ".jpg")]
    [InlineData(FileMimeTypeNames.Image.Png, ".png")]
    [InlineData(FileMimeTypeNames.Image.Gif, ".gif")]
    [InlineData(FileMimeTypeNames.Image.Svg, ".svg")]
    [InlineData(FileMimeTypeNames.Image.Tiff, ".tif")]
    public async Task GenerateCorrectExtensionForImageMimeTypes(string mimeType, string expectedExtension)
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test.jpg",
            ContentType = mimeType
        };
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        // Act
        var (_, extension) = await nameGenerator.Generate(createUpload);

        // Assert
        extension.Should().Be(expectedExtension);
    }

    [Theory]
    [InlineData(FileMimeTypeNames.Application.Pdf, ".pdf")]
    [InlineData(FileMimeTypeNames.Application.Zip, ".zip")]
    [InlineData(FileMimeTypeNames.Application.Gzip, ".gzip")]
    [InlineData(FileMimeTypeNames.Text.Plain, ".txt")]
    [InlineData(FileMimeTypeNames.Text.Html, ".htm")]
    public async Task GenerateCorrectExtensionForNonImageMimeTypes(string mimeType, string expectedExtension)
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test.file",
            ContentType = mimeType
        };
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        // Act
        var (_, extension) = await nameGenerator.Generate(createUpload);

        // Assert
        extension.Should().Be(expectedExtension);
    }

    [Fact]
    public async Task UseOriginalFileExtensionWhenMimeTypeIsUnknown()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "document.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        // Act
        var (_, extension) = await nameGenerator.Generate(createUpload);

        // Assert
        extension.Should().Be(".docx");
    }

    [Fact]
    public async Task GenerateUniqueNameForEachCall()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test.jpg",
            ContentType = FileMimeTypeNames.Image.Jpeg
        };
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        guidFactory.SetupSequence(f => f.Create())
            .Returns(guid1)
            .Returns(guid2);

        // Act
        var (name1, _) = await nameGenerator.Generate(createUpload);
        var (name2, _) = await nameGenerator.Generate(createUpload);

        // Assert
        name1.Should().NotBe(name2);
    }

    [Fact]
    public async Task GenerateNameWithoutSpecialCharacters()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test file!@#$%^&*().jpg",
            ContentType = FileMimeTypeNames.Image.Jpeg
        };
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        // Act
        var (name, _) = await nameGenerator.Generate(createUpload);

        // Assert
        name.Should().MatchRegex("^[a-zA-Z0-9]+$", "name should only contain alphanumeric characters");
    }

    [Fact]
    public async Task GenerateNameBasedOnOriginalFileName()
    {
        // Arrange
        var fileName = "my-unique-file-name.jpg";
        var createUpload1 = new CreateUpload
        {
            FileName = fileName,
            ContentType = FileMimeTypeNames.Image.Jpeg
        };
        var createUpload2 = new CreateUpload
        {
            FileName = fileName,
            ContentType = FileMimeTypeNames.Image.Jpeg
        };
        var fixedGuid = Guid.NewGuid();
        guidFactory.Setup(f => f.Create()).Returns(fixedGuid);

        // Act
        var (name1, _) = await nameGenerator.Generate(createUpload1);
        var (name2, _) = await nameGenerator.Generate(createUpload2);

        // Assert
        name1.Should().Be(name2, "same filename and salt should produce same name");
    }
}
