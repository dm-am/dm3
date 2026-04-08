using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Storage;

public class UploadFactoryShould : UnitTestBase
{
    private readonly UploadFactory _factory;
    private readonly Mock<IGuidFactory> _guidFactory;

    public UploadFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();

        _factory = new UploadFactory(_guidFactory.Object);
    }

    [Fact]
    public void CreateUploadWithValidProperties()
    {
        var uploadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var filePath = "/uploads/test.jpg";

        var createUpload = new CreateUpload
        {
            EntityId = entityId,
            FileName = "test.jpg"
        };

        _guidFactory.Setup(f => f.Create()).Returns(uploadId);

        var result = _factory.Create(createUpload, filePath, userId, original: true, createdAt);

        result.Should().NotBeNull();
        result.UploadId.Should().Be(uploadId);
        result.CreatedUtc.Should().Be(createdAt);
        result.UserId.Should().Be(userId);
        result.EntityId.Should().Be(entityId);
        result.FileName.Should().Be(createUpload.FileName);
        result.FilePath.Should().Be(filePath);
        result.Original.Should().BeTrue();
        result.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public void CreateOriginalUpload()
    {
        var createUpload = new CreateUpload
        {
            EntityId = Guid.NewGuid(),
            FileName = "original.png"
        };

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(createUpload, "/path/original.png", Guid.NewGuid(), original: true, DateTimeOffset.UtcNow);

        result.Original.Should().BeTrue();
    }

    [Fact]
    public void CreateNonOriginalUpload()
    {
        var createUpload = new CreateUpload
        {
            EntityId = Guid.NewGuid(),
            FileName = "thumbnail.png"
        };

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(createUpload, "/path/thumbnail.png", Guid.NewGuid(), original: false, DateTimeOffset.UtcNow);

        result.Original.Should().BeFalse();
    }

    [Fact]
    public void PreserveFilePath()
    {
        var filePath = "/uploads/images/2024/01/test-file.jpg";
        var createUpload = new CreateUpload
        {
            EntityId = Guid.NewGuid(),
            FileName = "test-file.jpg"
        };

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(createUpload, filePath, Guid.NewGuid(), true, DateTimeOffset.UtcNow);

        result.FilePath.Should().Be(filePath);
    }

    [Fact]
    public void CreateNonRemovedUpload()
    {
        var createUpload = new CreateUpload
        {
            EntityId = Guid.NewGuid(),
            FileName = "test.jpg"
        };

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(createUpload, "/path/test.jpg", Guid.NewGuid(), true, DateTimeOffset.UtcNow);

        result.IsRemoved.Should().BeFalse();
    }
}
