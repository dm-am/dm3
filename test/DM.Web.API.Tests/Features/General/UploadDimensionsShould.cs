using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Domain.Personal.Features.Profiles;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.General.Upload;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// An upload records how large its picture is, and the record travels to the
/// client.
/// </summary>
/// <remarks>
/// The source file preserves the aspect ratio of whatever was uploaded, so the
/// only variant whose shape nobody can predict is the original — and the profile
/// page draws exactly that one. With no dimensions on the wire the page had to
/// declare a square and hold it as a floor, which reserved 220 px for a picture
/// 165 px tall and left the difference blank under every landscape avatar. The
/// pair below is what lets the browser reserve the real box before it decodes.
///
/// Both ends are held here: the write, where the pipeline's measurement has to
/// reach the row, and the read, where it has to reach the DTO and only for the
/// variant it describes.
/// </remarks>
public class UploadDimensionsShould : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task RecordTheDimensionsTheImagePipelineMeasured()
    {
        var written = new List<NewUpload>();
        var repository = Mock<IUploadRepository>();
        repository.Setup(r => r.AddAsync(Capture.In(written)))
            .ReturnsAsync(new StoredUpload());

        var service = Service(repository, ProcessedTo(width: 640, height: 480));

        await service.DirectUpload(File(), UploadType.UserAvatar, _userId);

        written.Should().ContainSingle();
        written[0].Width.Should().Be(640);
        written[0].Height.Should().Be(480);
    }

    [Fact]
    public void PublishTheDimensionsOfTheOriginalOnly()
    {
        var picture = Mapper().Map<UserPicture>(new AvatarPicture
        {
            SourceObjectKey = "avatars/key.jpg",
            SourceUrl = "https://cdn.example/avatars/key.jpg",
            SourceWidth = 200,
            SourceHeight = 150,
        });

        picture.OriginalWidth.Should().Be(200);
        picture.OriginalHeight.Should().Be(150);
    }

    /// <summary>
    /// An upload stored before the pipeline measured anything has no pair, and
    /// the DTO says so instead of inventing one. A guessed ratio is worse than
    /// an absent one: the client can fall back to its own reservation only while
    /// it can tell that nobody measured.
    /// </summary>
    [Fact]
    public void SayNothingAboutAPictureNobodyMeasured()
    {
        var picture = Mapper().Map<UserPicture>(new AvatarPicture
        {
            SourceObjectKey = "avatars/legacy.jpg",
            SourceUrl = "https://cdn.example/avatars/legacy.jpg",
        });

        picture.OriginalUrl.Should().NotBeNull("the picture itself is still there");
        picture.OriginalWidth.Should().BeNull();
        picture.OriginalHeight.Should().BeNull();
    }

    /// <summary>
    /// The mapper as the host builds it, so the registration of the converter is
    /// part of what these two assert.
    /// </summary>
    private IMapper Mapper()
    {
        var imgproxy = Mock<IImgproxyUrlBuilder>();
        imgproxy.Setup(b => b.BuildSquareThumbnail(It.IsAny<string>(), It.IsAny<int>()))
            .Returns("https://cdn.example/thumb");

        return new MapperConfiguration(cfg => cfg.AddProfile<UserMappingProfile>())
            .CreateMapper(type => type == typeof(AvatarPictureConverter)
                ? new AvatarPictureConverter(imgproxy.Object)
                : Activator.CreateInstance(type)!);
    }

    private Mock<IImageProcessingService> ProcessedTo(int width, int height)
    {
        var imageProcessing = Mock<IImageProcessingService>();
        imageProcessing
            .Setup(s => s.ProcessAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedImage(new byte[] { 1, 2, 3 }, "image/png", ".png", width, height));
        return imageProcessing;
    }

    private UploadApiService Service(
        Mock<IUploadRepository> repository,
        Mock<IImageProcessingService> imageProcessing)
    {
        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(Identities.User(_userId));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(p => p.Now).Returns(DateTimeOffset.UtcNow);

        return new UploadApiService(
            repository.Object,
            identityProvider.Object,
            Mock<IIntentionManager>().Object,
            Mock<IUserService>().Object,
            dateTimeProvider.Object,
            Mock<IObjectStorage>().Object,
            imageProcessing.Object,
            Mock<ICache>().Object,
            Mock<IHttpContextAccessor>().Object,
            new IUploadTargetAuthorizer[] { new AllowingAuthorizer(UploadType.UserAvatar) },
            Options.Create(new CdnConfiguration
            {
                BucketName = "dm-test",
                PublicUrl = "https://cdn.example",
            }));
    }

    private static IFormFile File()
    {
        var bytes = Encoding.UTF8.GetBytes("not a real image; processing is mocked");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "avatar.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };
    }

    private sealed class AllowingAuthorizer : IUploadTargetAuthorizer
    {
        public AllowingAuthorizer(UploadType type) => Type = type;

        public UploadType Type { get; }

        public Task EnsureAllowedAsync(Guid targetId) => Task.CompletedTask;
    }
}
