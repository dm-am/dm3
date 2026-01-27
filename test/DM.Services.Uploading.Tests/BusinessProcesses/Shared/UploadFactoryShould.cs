using System;
using DM.Services.Core.Implementation;
using DM.Services.Uploading.BusinessProcesses.Shared;
using DM.Services.Uploading.Dto;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Services.Uploading.Tests.BusinessProcesses.Shared;

public class UploadFactoryShould : UnitTestBase
{
    private readonly Mock<IGuidFactory> guidFactory;
    private readonly UploadFactory factory;

    public UploadFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();
        factory = new UploadFactory(guidFactory.Object);
    }

    [Fact]
    public void CreateUploadWithAllRequiredProperties()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var createUpload = new CreateUpload
        {
            FileName = "test-image.jpg",
            EntityId = entityId
        };
        guidFactory.Setup(f => f.Create()).Returns(uploadId);

        // Act
        var result = factory.Create(createUpload, "/uploads/abc123.jpg", userId, true, createdAt);

        // Assert
        result.Should().NotBeNull();
        result.UploadId.Should().Be(uploadId);
        result.UserId.Should().Be(userId);
        result.EntityId.Should().Be(entityId);
        result.FileName.Should().Be("test-image.jpg");
        result.FilePath.Should().Be("/uploads/abc123.jpg");
        result.Original.Should().BeTrue();
        result.CreatedUtc.Should().Be(createdAt);
        result.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public void CreateUploadMarkedAsNotOriginal()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "thumbnail.jpg",
            EntityId = Guid.NewGuid()
        };
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        // Act
        var result = factory.Create(
            createUpload,
            "/uploads/thumb.jpg",
            Guid.NewGuid(),
            false,
            DateTimeOffset.UtcNow);

        // Assert
        result.Original.Should().BeFalse();
    }

    [Fact]
    public void CreateUploadWithNullEntityId()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "orphan-image.jpg",
            EntityId = null
        };
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        // Act
        var result = factory.Create(
            createUpload,
            "/uploads/orphan.jpg",
            Guid.NewGuid(),
            true,
            DateTimeOffset.UtcNow);

        // Assert
        result.EntityId.Should().BeNull();
    }

    [Fact]
    public void GenerateUniqueUploadIdForEachCreation()
    {
        // Arrange
        var uploadId1 = Guid.NewGuid();
        var uploadId2 = Guid.NewGuid();
        guidFactory.SetupSequence(f => f.Create())
            .Returns(uploadId1)
            .Returns(uploadId2);
        var createUpload = new CreateUpload
        {
            FileName = "test.jpg",
            EntityId = Guid.NewGuid()
        };

        // Act
        var result1 = factory.Create(createUpload, "/path1.jpg", Guid.NewGuid(), true, DateTimeOffset.UtcNow);
        var result2 = factory.Create(createUpload, "/path2.jpg", Guid.NewGuid(), true, DateTimeOffset.UtcNow);

        // Assert
        result1.UploadId.Should().Be(uploadId1);
        result2.UploadId.Should().Be(uploadId2);
        result1.UploadId.Should().NotBe(result2.UploadId);
    }

    [Fact]
    public void AlwaysCreateUploadWithIsRemovedSetToFalse()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test.jpg",
            EntityId = Guid.NewGuid()
        };
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        // Act
        var result = factory.Create(
            createUpload,
            "/uploads/test.jpg",
            Guid.NewGuid(),
            true,
            DateTimeOffset.UtcNow);

        // Assert
        result.IsRemoved.Should().BeFalse();
    }
}
