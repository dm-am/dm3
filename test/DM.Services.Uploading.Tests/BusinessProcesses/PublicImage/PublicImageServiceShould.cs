using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Implementation;
using DM.Services.Uploading.BusinessProcesses.Cdn;
using DM.Services.Uploading.BusinessProcesses.PublicImage;
using DM.Services.Uploading.BusinessProcesses.Shared;
using DM.Services.Uploading.Dto;
using DM.Tests.Core;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using DbUpload = DM.Services.DataAccess.BusinessObjects.Common.Upload;

namespace DM.Services.Uploading.Tests.BusinessProcesses.PublicImage;

public class PublicImageServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateUpload>> validator;
    private readonly Mock<INameGenerator> nameGenerator;
    private readonly Mock<IDateTimeProvider> dateTimeProvider;
    private readonly Mock<IUploader> uploader;
    private readonly Mock<IUploadFactory> uploadFactory;
    private readonly Mock<IPublicImageUploadRepository> repository;
    private readonly Mock<IIdentityProvider> identityProvider;
    private readonly PublicImageService service;

    public PublicImageServiceShould()
    {
        validator = Mock<IValidator<CreateUpload>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateUpload>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        nameGenerator = Mock<INameGenerator>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        uploader = Mock<IUploader>();
        uploadFactory = Mock<IUploadFactory>();
        repository = Mock<IPublicImageUploadRepository>();
        identityProvider = Mock<IIdentityProvider>();

        service = new PublicImageService(
            validator.Object,
            nameGenerator.Object,
            dateTimeProvider.Object,
            uploader.Object,
            uploadFactory.Object,
            repository.Object,
            identityProvider.Object);
    }

    [Fact]
    public async Task ValidateUploadBeforeProcessing()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test.jpg",
            StreamAccessor = CreateTestImageStream
        };

        SetupSuccessfulUpload(createUpload);

        // Act
        await service.Upload(createUpload);

        // Assert
        validator.Verify(v => v.ValidateAsync(
            It.Is<ValidationContext<CreateUpload>>(ctx => ctx.InstanceToValidate == createUpload),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateUniqueFileNameForUpload()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test.jpg",
            StreamAccessor = CreateTestImageStream
        };

        SetupSuccessfulUpload(createUpload);

        // Act
        await service.Upload(createUpload);

        // Assert
        nameGenerator.Verify(n => n.Generate(createUpload), Times.Once);
    }

    [Fact]
    public async Task UploadThreeVersionsOfImage()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "avatar.jpg",
            StreamAccessor = CreateTestImageStream
        };

        SetupSuccessfulUpload(createUpload);

        // Act
        await service.Upload(createUpload);

        // Assert
        uploader.Verify(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name.jpg"), Times.Once,
            "should upload original");
        uploader.Verify(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name_m.jpg"), Times.Once,
            "should upload medium version");
        uploader.Verify(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name_s.jpg"), Times.Once,
            "should upload small version");
    }

    [Fact]
    public async Task CreateDatabaseRecordsForAllThreeVersions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var createUpload = new CreateUpload
        {
            FileName = "profile.jpg",
            EntityId = entityId,
            StreamAccessor = CreateTestImageStream
        };

        SetupSuccessfulUpload(createUpload);

        // Override the SetupSuccessfulUpload with specific values for this test
        var user = new DM.Services.Authentication.Dto.AuthenticatedUser { UserId = userId };
        var identity = Mock<DM.Services.Authentication.Dto.IIdentity>();
        identity.Setup(i => i.User).Returns(user);
        identityProvider.Setup(p => p.Current).Returns(identity.Object);
        dateTimeProvider.Setup(p => p.Now).Returns(now);

        // Act
        await service.Upload(createUpload);

        // Assert
        uploadFactory.Verify(f => f.Create(
            It.IsAny<CreateUpload>(),
            "https://cdn.test/original.jpg",
            userId,
            true,
            now), Times.Once, "should create record for original");

        uploadFactory.Verify(f => f.Create(
            It.IsAny<CreateUpload>(),
            "https://cdn.test/medium.jpg",
            userId,
            false,
            now), Times.Once, "should create record for medium");

        uploadFactory.Verify(f => f.Create(
            It.IsAny<CreateUpload>(),
            "https://cdn.test/small.jpg",
            userId,
            false,
            now), Times.Once, "should create record for small");
    }

    [Fact]
    public async Task SaveAllUploadRecordsToRepository()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test.jpg",
            StreamAccessor = CreateTestImageStream
        };

        var dbUploads = new List<DbUpload>();
        uploadFactory
            .Setup(f => f.Create(
                It.IsAny<CreateUpload>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<DateTimeOffset>()))
            .Returns((CreateUpload cu, string path, Guid uid, bool orig, DateTimeOffset dt) =>
            {
                var upload = new DbUpload { FilePath = path, Original = orig };
                dbUploads.Add(upload);
                return upload;
            });

        SetupSuccessfulUpload(createUpload);

        // Act
        await service.Upload(createUpload);

        // Assert
        repository.Verify(r => r.Create(
            It.Is<IEnumerable<DbUpload>>(uploads => uploads.Count() == 3)),
            Times.Once);
    }

    [Fact]
    public async Task ReturnCorrectlyOrderedUploadTuple()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test.jpg",
            StreamAccessor = CreateTestImageStream
        };

        var originalUpload = new Upload { FilePath = "https://cdn.test/original.jpg" };
        var mediumUpload = new Upload { FilePath = "https://cdn.test/medium.jpg" };
        var smallUpload = new Upload { FilePath = "https://cdn.test/small.jpg" };

        nameGenerator.Setup(n => n.Generate(It.IsAny<CreateUpload>()))
            .ReturnsAsync(("test-name", ".jpg"));
        uploader.Setup(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name.jpg"))
            .ReturnsAsync("https://cdn.test/original.jpg");
        uploader.Setup(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name_m.jpg"))
            .ReturnsAsync("https://cdn.test/medium.jpg");
        uploader.Setup(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name_s.jpg"))
            .ReturnsAsync("https://cdn.test/small.jpg");

        repository.Setup(r => r.Create(It.IsAny<IEnumerable<DbUpload>>()))
            .ReturnsAsync(new[] { originalUpload, mediumUpload, smallUpload });

        var user = new DM.Services.Authentication.Dto.AuthenticatedUser { UserId = Guid.NewGuid() };
        var identity = Mock<DM.Services.Authentication.Dto.IIdentity>();
        identity.Setup(i => i.User).Returns(user);
        identityProvider.Setup(p => p.Current).Returns(identity.Object);
        dateTimeProvider.Setup(p => p.Now).Returns(DateTimeOffset.UtcNow);

        // Act
        var (original, medium, small) = await service.Upload(createUpload);

        // Assert
        original.Should().Be(originalUpload);
        medium.Should().Be(mediumUpload);
        small.Should().Be(smallUpload);
    }

    [Fact]
    public async Task PrepareObsoleteUploadsForDeletion()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Act
        await service.PrepareObsoleteForDeleting(entityId);

        // Assert
        repository.Verify(r => r.RemoveObsoleteUploads(entityId), Times.Once);
    }

    private Stream CreateTestImageStream()
    {
        // Create a simple 300x300 test image
        var image = new Image<Rgba32>(300, 300);
        var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        stream.Position = 0;
        return stream;
    }

    private void SetupSuccessfulUpload(CreateUpload createUpload)
    {
        nameGenerator.Setup(n => n.Generate(createUpload))
            .ReturnsAsync(("test-name", ".jpg"));

        uploader.Setup(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name.jpg"))
            .ReturnsAsync("https://cdn.test/original.jpg");
        uploader.Setup(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name_m.jpg"))
            .ReturnsAsync("https://cdn.test/medium.jpg");
        uploader.Setup(u => u.Upload(It.IsAny<Func<Stream>>(), "test-name_s.jpg"))
            .ReturnsAsync("https://cdn.test/small.jpg");

        uploadFactory.Setup(f => f.Create(
                It.IsAny<CreateUpload>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<DateTimeOffset>()))
            .Returns((CreateUpload cu, string path, Guid uid, bool orig, DateTimeOffset dt) =>
                new DbUpload { FilePath = path, Original = orig });

        repository.Setup(r => r.Create(It.IsAny<IEnumerable<DbUpload>>()))
            .ReturnsAsync((IEnumerable<DbUpload> uploads) =>
                uploads.Select(u => new Upload { FilePath = u.FilePath }).ToList());

        var user = new DM.Services.Authentication.Dto.AuthenticatedUser { UserId = Guid.NewGuid() };
        var identity = Mock<DM.Services.Authentication.Dto.IIdentity>();
        identity.Setup(i => i.User).Returns(user);
        identityProvider.Setup(p => p.Current).Returns(identity.Object);
        dateTimeProvider.Setup(p => p.Now).Returns(DateTimeOffset.UtcNow);
    }
}
