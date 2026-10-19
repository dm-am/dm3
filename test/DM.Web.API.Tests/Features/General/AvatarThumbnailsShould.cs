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
using DM.Web.API.Features.General.Upload;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// The thumbnails of an avatar go into the store beside it when it is uploaded, leave with it when its
/// row does not go in, and are handed out from beside its address.
/// </summary>
public class AvatarThumbnailsShould : UnitTestBase
{
    private static readonly byte[] Original = [1, 2, 3];

    private static readonly ProcessedThumbnail[] Thumbnails =
        [new(AvatarThumbnail.SmallSize, [4]), new(AvatarThumbnail.MediumSize, [5])];

    private readonly Guid _userId = Guid.NewGuid();
    private readonly IObjectStorage _objectStorage = Mock<IObjectStorage>();
    private readonly IUploadRepository _repository = Mock<IUploadRepository>();

    [Fact]
    public async Task GoIntoTheStoreBesideTheOriginal()
    {
        var written = new List<NewUpload>();
        _repository.AddAsync(Arg.Do<NewUpload>(written.Add)).Returns(new StoredUpload());

        await Service().DirectUpload(Uploads.File(), UploadType.UserAvatar, _userId, UploadAudience.All);

        var objectKey = written.Should().ContainSingle().Which.ObjectKey;
        await _objectStorage.Received(1).PutAsync(objectKey, Original, "image/png", Arg.Any<CancellationToken>());
        foreach (var thumbnail in Thumbnails)
        {
            await _objectStorage.Received(1).PutAsync(
                AvatarThumbnail.AddressOf(objectKey, thumbnail.Size),
                thumbnail.Bytes,
                AvatarThumbnail.ContentType,
                Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task LeaveTheStoreWithTheOriginalWhenItsRowDoesNotGoIn()
    {
        var put = new List<string>();
        _objectStorage.PutAsync(Arg.Do<string>(put.Add), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var deleted = new List<string>();
        _objectStorage.DeleteAsync(Arg.Do<string>(deleted.Add), Arg.Any<CancellationToken>()).Returns(true);
        _repository.AddAsync(Arg.Any<NewUpload>()).ThrowsAsync(new InvalidOperationException("The row did not go in"));

        var act = () => Service().DirectUpload(Uploads.File(), UploadType.UserAvatar, _userId, UploadAudience.All);

        await act.Should().ThrowAsync<InvalidOperationException>();
        put.Should().HaveCount(1 + Thumbnails.Length);
        deleted.Should().BeEquivalentTo(put, "no row names any of them, so the sweeper would never find one");
    }

    [Fact]
    public void BeHandedOutFromBesideTheAddressOfTheOriginal()
    {
        var picture = new UserMapper().ToUserPicture(new AvatarPicture
        {
            SourceObjectKey = "avatars/u_g.jpg",
            SourceUrl = "https://cdn.example/dm-uploads/avatars/u_g.jpg",
        });

        picture.SmallUrl.Should().Be("https://cdn.example/dm-uploads/avatars/u_g_100.webp");
        picture.MediumUrl.Should().Be("https://cdn.example/dm-uploads/avatars/u_g_400.webp");
        picture.OriginalUrl.Should().Be("https://cdn.example/dm-uploads/avatars/u_g.jpg");
    }

    private UploadApiService Service()
    {
        var imageProcessing = Mock<IImageProcessingService>();
        imageProcessing
            .ProcessAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessedImage(Original, "image/png", ".png", 640, 480) { Thumbnails = Thumbnails });

        return Uploads.Service(
            _userId,
            _repository,
            imageProcessing,
            _objectStorage,
            Mock<IIntentionManager>(),
            new AllowingUploadAuthorizer(UploadType.UserAvatar));
    }
}
