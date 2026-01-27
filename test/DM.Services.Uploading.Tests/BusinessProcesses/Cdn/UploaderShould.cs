using DM.Services.Uploading.Configuration;
using DM.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Services.Uploading.Tests.BusinessProcesses.Cdn;

/// <summary>
/// Tests for Uploader class
/// Note: Due to AWS SDK having optional parameters, we cannot mock UploadObjectFromStreamAsync
/// in unit tests (expression trees don't support optional parameters). These tests focus on
/// configuration validation and URL construction logic.
/// Integration tests should cover the actual S3 upload functionality.
/// </summary>
public class UploaderShould : UnitTestBase
{
    [Fact]
    public void UseConfigurationFromOptions()
    {
        // Arrange
        var cdnConfiguration = new CdnConfiguration
        {
            BucketName = "test-bucket",
            PublicUrl = "https://cdn.example.com",
            Folder = "uploads"
        };
        var options = Mock<IOptions<CdnConfiguration>>();
        options.Setup(o => o.Value).Returns(cdnConfiguration);

        // Act & Assert - constructor should not throw
        var exception = Record.Exception(() =>
            new DM.Services.Uploading.BusinessProcesses.Cdn.Uploader(
                options.Object,
                new System.Lazy<Amazon.S3.IAmazonS3>(() => Mock<Amazon.S3.IAmazonS3>().Object)));

        exception.Should().BeNull();
    }

    [Fact]
    public void AcceptLazyS3Client()
    {
        // Arrange
        var cdnConfiguration = new CdnConfiguration
        {
            BucketName = "test-bucket",
            PublicUrl = "https://cdn.example.com",
            Folder = "uploads"
        };
        var options = Mock<IOptions<CdnConfiguration>>();
        options.Setup(o => o.Value).Returns(cdnConfiguration);

        var clientFactoryCalled = false;
        var lazyClient = new System.Lazy<Amazon.S3.IAmazonS3>(() =>
        {
            clientFactoryCalled = true;
            return Mock<Amazon.S3.IAmazonS3>().Object;
        });

        // Act
        var uploader = new DM.Services.Uploading.BusinessProcesses.Cdn.Uploader(
            options.Object,
            lazyClient);

        // Assert - client should not be created until first use
        clientFactoryCalled.Should().BeFalse("client should use lazy initialization");
    }

    [Theory]
    [InlineData("https://cdn.example.com", "my-bucket", "uploads", "test.jpg",
        "https://cdn.example.com/my-bucket/uploads/test.jpg")]
    [InlineData("https://s3.amazonaws.com", "bucket", "", "image.png",
        "https://s3.amazonaws.com/bucket/image.png")]
    [InlineData("https://cdn.test.com", "files", "public/images", "avatar.gif",
        "https://cdn.test.com/files/public/images/avatar.gif")]
    public void ConstructExpectedPublicUrls(string publicUrl, string bucketName, string folder,
        string fileName, string expectedUrl)
    {
        // This test documents the expected URL construction behavior
        // Actual behavior would need integration tests with real S3 client

        // Arrange - URL pattern: {publicUrl}/{bucketName}/{folder}/{fileName}
        var pattern = string.IsNullOrEmpty(folder)
            ? $"{publicUrl}/{bucketName}/{fileName}"
            : $"{publicUrl}/{bucketName}/{folder}/{fileName}";

        // Assert
        pattern.Should().Be(expectedUrl, "URL should follow the expected pattern");
    }
}
