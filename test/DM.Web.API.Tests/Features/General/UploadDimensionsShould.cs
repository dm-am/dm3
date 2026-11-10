using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

// The original keeps the aspect ratio of whatever was uploaded, and the page reserves its box from
// the pair.
public class UploadDimensionsShould : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task RecordTheDimensionsTheImagePipelineMeasured()
    {
        var written = new List<NewUpload>();
        var repository = Mock<IUploadRepository>();
        repository.AddAsync(Arg.Do<NewUpload>(written.Add)).Returns(new StoredUpload());

        var service = Uploads.Service(
            _userId,
            repository,
            ProcessedTo(width: 640, height: 480),
            Mock<IObjectStorage>(),
            Mock<IIntentionManager>(),
            new AllowingUploadAuthorizer(UploadType.UserAvatar));

        await service.DirectUpload(Uploads.File(), UploadType.UserAvatar, _userId, UploadAudience.All);

        written.Should().ContainSingle();
        written[0].Width.Should().Be(640);
        written[0].Height.Should().Be(480);
    }

    [Fact]
    public void PublishTheDimensionsOfTheOriginalOnly()
    {
        var picture = Mapper().ToUserPicture(new AvatarPicture
        {
            SourceObjectKey = "avatars/key.jpg",
            SourceUrl = "https://cdn.example/avatars/key.jpg",
            SourceWidth = 200,
            SourceHeight = 150,
        });

        picture.OriginalWidth.Should().Be(200);
        picture.OriginalHeight.Should().Be(150);
    }

    [Fact]
    public void SayNothingAboutAPictureNobodyMeasured()
    {
        var picture = Mapper().ToUserPicture(new AvatarPicture
        {
            SourceObjectKey = "avatars/legacy.jpg",
            SourceUrl = "https://cdn.example/avatars/legacy.jpg",
        });

        picture.OriginalUrl.Should().NotBeNull("the picture itself is still there");
        picture.OriginalWidth.Should().BeNull();
        picture.OriginalHeight.Should().BeNull();
    }

    private static UserMapper Mapper() => new();

    private static IImageProcessingService ProcessedTo(int width, int height)
    {
        var imageProcessing = Mock<IImageProcessingService>();
        imageProcessing
            .ProcessAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessedImage(new byte[] { 1, 2, 3 }, "image/png", ".png", width, height));
        return imageProcessing;
    }
}
