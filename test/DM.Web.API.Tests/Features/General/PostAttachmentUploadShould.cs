using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.General.Upload;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// The rules a post attachment answers to that an avatar does not: its own size
/// ceiling, a limit on how many one post carries, and a removal right the file's
/// own ownership rule does not reach.
/// </summary>
/// <remarks>
/// Both limits are asserted with the pipeline and the bucket untouched: the orphan sweeper walks rows, and a
/// refused request writes none.
/// </remarks>
public class PostAttachmentUploadShould : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();
    private readonly IObjectStorage _objectStorage;
    private readonly IImageProcessingService _imageProcessing;
    private readonly IUploadRepository _repository;
    private readonly IIntentionManager _intentionManager;

    public PostAttachmentUploadShould()
    {
        _objectStorage = Mock<IObjectStorage>();

        _imageProcessing = Mock<IImageProcessingService>();
        _imageProcessing
            .ProcessAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>()).Returns(new ProcessedImage([1, 2, 3], "image/png", ".png", 1600, 1200));

        _repository = Mock<IUploadRepository>();
        _repository.AddAsync(Arg.Any<NewUpload>()).Returns(ci => Uploads.Stored(ci.ArgAt<NewUpload>(0)));

        _intentionManager = Mock<IIntentionManager>();
    }

    [Fact]
    public async Task RefuseAFileOverFiveMegabytes()
    {
        var service = Service(new AllowingUploadAuthorizer(UploadType.PostAttachment));

        var act = () => service.DirectUpload(
            Uploads.File(UploadPolicy.MaxPostAttachmentSizeBytes + 1), UploadType.PostAttachment, _postId, UploadAudience.All);

        await act.Should().ThrowAsync<HttpBadRequestException>();
        VerifyNothingWasProcessedOrStored();
    }

    /// <summary>
    /// The same file as an avatar, where the ceiling is the request one. The
    /// limit belongs to the type, not to the endpoint.
    /// </summary>
    [Fact]
    public async Task StillAcceptTheSameSizeAsAnAvatar()
    {
        var service = Service(new AllowingUploadAuthorizer(UploadType.UserAvatar));

        var act = () => service.DirectUpload(
            Uploads.File(UploadPolicy.MaxPostAttachmentSizeBytes + 1), UploadType.UserAvatar, _userId, UploadAudience.All);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefuseAFourthFileOnAPost()
    {
        _repository.CountPostAttachmentsAsync(_postId).Returns(UploadPolicy.MaxPostAttachments);
        var service = Service(new AllowingUploadAuthorizer(UploadType.PostAttachment));

        var act = () => service.DirectUpload(Uploads.File(), UploadType.PostAttachment, _postId, UploadAudience.All);

        await act.Should().ThrowAsync<HttpBadRequestException>();
        VerifyNothingWasProcessedOrStored();
    }

    [Fact]
    public async Task AcceptTheLastFileTheLimitAllows()
    {
        _repository.CountPostAttachmentsAsync(_postId).Returns(UploadPolicy.MaxPostAttachments - 1);
        var service = Service(new AllowingUploadAuthorizer(UploadType.PostAttachment));

        var act = () => service.DirectUpload(Uploads.File(), UploadType.PostAttachment, _postId, UploadAudience.All);

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// The count is a question about one post, so it is never asked about a type
    /// that has no such limit.
    /// </summary>
    [Fact]
    public async Task NotCountAnythingForAnAvatar()
    {
        var service = Service(new AllowingUploadAuthorizer(UploadType.UserAvatar));

        await service.DirectUpload(Uploads.File(), UploadType.UserAvatar, _userId, UploadAudience.All);

        await _repository.DidNotReceive().CountPostAttachmentsAsync(Arg.Any<Guid>());
    }

    /// <summary>
    /// The object lands under the prefix of its type and nothing before it: the
    /// bucket policy grants anonymous reads by that prefix.
    /// </summary>
    [Fact]
    public async Task StoreTheFileUnderThePrefixOfItsType()
    {
        var service = Service(new AllowingUploadAuthorizer(UploadType.UserAvatar));

        await service.DirectUpload(Uploads.File(), UploadType.UserAvatar, _userId, UploadAudience.All);

        var prefix = $"{UploadFolder.For(UploadType.UserAvatar)}/{_userId:N}_";
        await _objectStorage.Received(1).PutAsync(
            Arg.Is<string>(key => key.StartsWith(prefix, StringComparison.Ordinal)),
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// No public address for a prefix the bucket answers to nobody, and the
    /// content endpoint in its place.
    /// </summary>
    /// <remarks>A URL here would be a link that answers 403.</remarks>
    [Fact]
    public async Task AnswerWithNoPublicUrlAndWithTheContentEndpoint()
    {
        var service = Service(new AllowingUploadAuthorizer(UploadType.PostAttachment));

        var result = await service.DirectUpload(Uploads.File(), UploadType.PostAttachment, _postId, UploadAudience.All);

        result.Url.Should().BeNull();
        result.ContentUrl.Should().Be($"/v1/uploads/{result.Id:D}/content");
        _objectStorage.DidNotReceive().BuildPublicUrl(Arg.Any<string>());
    }

    /// <summary>
    /// The master of a game did not upload the file and is not a site moderator,
    /// so the upload's own delete rule refuses them. The post they may edit is
    /// what grants it.
    /// </summary>
    [Fact]
    public async Task LetAnEditorOfThePostRemoveTheFileTheyDoNotOwn()
    {
        var uploadId = Guid.NewGuid();
        StoredAttachment(uploadId);
        _intentionManager
            .IsAllowed(UploadIntention.Delete, Arg.Any<StoredUpload>()).Returns(false);
        _intentionManager
            .When(m => m.ThrowIfForbidden(UploadIntention.Delete, Arg.Any<StoredUpload>()))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав для этого действия"));
        var service = Service(new AllowingUploadAuthorizer(UploadType.PostAttachment, mayDetach: true));

        await service.DeleteUpload(uploadId);

        await _repository.Received(1).SoftDeleteAsync(uploadId, _userId, Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task RefuseRemovalBySomebodyWhoNeitherOwnsTheFileNorMayEditThePost()
    {
        var uploadId = Guid.NewGuid();
        StoredAttachment(uploadId);
        _intentionManager
            .IsAllowed(UploadIntention.Delete, Arg.Any<StoredUpload>()).Returns(false);
        _intentionManager
            .When(m => m.ThrowIfForbidden(UploadIntention.Delete, Arg.Any<StoredUpload>()))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав для этого действия"));
        var service = Service(new AllowingUploadAuthorizer(UploadType.PostAttachment, mayDetach: false));

        var act = () => service.DeleteUpload(uploadId);

        await act.Should().ThrowAsync<HttpException>();
        await _repository.DidNotReceive().SoftDeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset>());
    }

    /// <summary>
    /// The owner and Moderator+ keep the right they already had, and the entity's
    /// rule is not even consulted for them.
    /// </summary>
    [Fact]
    public async Task LetTheOwnerRemoveTheirOwnFileWithoutAskingThePost()
    {
        var uploadId = Guid.NewGuid();
        StoredAttachment(uploadId);
        _intentionManager
            .IsAllowed(UploadIntention.Delete, Arg.Any<StoredUpload>()).Returns(true);
        var refusing = new AllowingUploadAuthorizer(UploadType.PostAttachment, mayDetach: false);
        var service = Service(refusing);

        await service.DeleteUpload(uploadId);

        refusing.DetachAsked.Should().BeFalse();
        await _repository.Received(1).SoftDeleteAsync(uploadId, _userId, Arg.Any<DateTimeOffset>());
    }

    private void StoredAttachment(Guid uploadId) =>
        _repository.GetAsync(uploadId).Returns(new StoredUpload
        {
            Id = uploadId,
            UserId = Guid.NewGuid(),
            Type = UploadType.PostAttachment,
            TargetId = _postId,
            FileName = "karta.png",
            ContentType = "image/png",
            Status = UploadStatus.Confirmed,
        });

    private UploadApiService Service(params IUploadTargetAuthorizer[] authorizers) =>
        Uploads.Service(_userId, _repository, _imageProcessing, _objectStorage, _intentionManager, authorizers);

    private void VerifyNothingWasProcessedOrStored()
    {
        _imageProcessing.DidNotReceive().ProcessAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>());
        _objectStorage.DidNotReceive().PutAsync(
                Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _repository.DidNotReceive().AddAsync(Arg.Any<NewUpload>());
    }
}
