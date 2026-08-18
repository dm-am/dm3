using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Domain.Personal.Features.Profiles;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Web.API.Features.General.Upload;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// The rules a post attachment answers to that an avatar does not: its own size
/// ceiling, a limit on how many one post carries, and a removal right the file's
/// own ownership rule does not reach.
/// </summary>
/// <remarks>
/// Both limits are asserted with the pipeline and the bucket verified untouched.
/// A refusal that arrives after the file has been decoded and written is a
/// refusal a caller can spend the server's work on, and the object left behind
/// is one nothing collects — the orphan sweeper walks rows, and a refused request
/// writes none.
/// </remarks>
public class PostAttachmentUploadShould : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();
    private readonly Mock<IObjectStorage> _objectStorage;
    private readonly Mock<IImageProcessingService> _imageProcessing;
    private readonly Mock<IUploadRepository> _repository;
    private readonly Mock<IIntentionManager> _intentionManager;

    public PostAttachmentUploadShould()
    {
        _objectStorage = Mock<IObjectStorage>();

        _imageProcessing = Mock<IImageProcessingService>();
        _imageProcessing
            .Setup(s => s.ProcessAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<UploadType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedImage([1, 2, 3], "image/png", ".png", 1600, 1200));

        _repository = Mock<IUploadRepository>();
        _repository.Setup(r => r.AddAsync(It.IsAny<NewUpload>()))
            .ReturnsAsync((NewUpload u) => new StoredUpload
            {
                Id = u.Id,
                UserId = u.UserId,
                Type = u.Type,
                TargetId = u.TargetId,
                FileName = u.FileName,
                ContentType = u.ContentType,
                SizeBytes = u.SizeBytes,
                Status = u.Status,
                Url = u.Url,
                CreatedUtc = u.CreatedUtc,
                ConfirmedUtc = u.ConfirmedUtc,
            });

        _intentionManager = Mock<IIntentionManager>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Limits
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefuseAFileOverFiveMegabytes()
    {
        var service = Service(new AllowingAuthorizer(UploadType.PostAttachment));

        var act = () => service.DirectUpload(
            File(UploadPolicy.MaxPostAttachmentSizeBytes + 1), UploadType.PostAttachment, _postId);

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
        var service = Service(new AllowingAuthorizer(UploadType.UserAvatar));

        var act = () => service.DirectUpload(
            File(UploadPolicy.MaxPostAttachmentSizeBytes + 1), UploadType.UserAvatar, _userId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefuseAFourthFileOnAPost()
    {
        _repository.Setup(r => r.CountPostAttachmentsAsync(_postId))
            .ReturnsAsync(UploadPolicy.MaxPostAttachments);
        var service = Service(new AllowingAuthorizer(UploadType.PostAttachment));

        var act = () => service.DirectUpload(File(), UploadType.PostAttachment, _postId);

        await act.Should().ThrowAsync<HttpBadRequestException>();
        VerifyNothingWasProcessedOrStored();
    }

    [Fact]
    public async Task AcceptTheLastFileTheLimitAllows()
    {
        _repository.Setup(r => r.CountPostAttachmentsAsync(_postId))
            .ReturnsAsync(UploadPolicy.MaxPostAttachments - 1);
        var service = Service(new AllowingAuthorizer(UploadType.PostAttachment));

        var act = () => service.DirectUpload(File(), UploadType.PostAttachment, _postId);

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// The count is a question about one post, so it is never asked about a type
    /// that has no such limit.
    /// </summary>
    [Fact]
    public async Task NotCountAnythingForAnAvatar()
    {
        var service = Service(new AllowingAuthorizer(UploadType.UserAvatar));

        await service.DirectUpload(File(), UploadType.UserAvatar, _userId);

        _repository.Verify(r => r.CountPostAttachmentsAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // What the answer says
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// No public address for a prefix the bucket answers to nobody, and the
    /// content endpoint in its place.
    /// </summary>
    /// <remarks>
    /// A URL here would be a 200 carrying a link that returns 403, which is worse
    /// than no link: the client renders a broken picture and the failure looks
    /// like the file rather than the policy.
    /// </remarks>
    [Fact]
    public async Task AnswerWithNoPublicUrlAndWithTheContentEndpoint()
    {
        var service = Service(new AllowingAuthorizer(UploadType.PostAttachment));

        var result = await service.DirectUpload(File(), UploadType.PostAttachment, _postId);

        result.Url.Should().BeNull();
        result.ContentUrl.Should().Be($"/v1/uploads/{result.Id:D}/content");
        _objectStorage.Verify(s => s.BuildPublicUrl(It.IsAny<string>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Removal
    // ─────────────────────────────────────────────────────────────────────────

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
            .Setup(m => m.IsAllowed(UploadIntention.Delete, It.IsAny<StoredUpload>()))
            .Returns(false);
        var service = Service(new AllowingAuthorizer(UploadType.PostAttachment, mayDetach: true));

        await service.DeleteUpload(uploadId);

        _repository.Verify(
            r => r.SoftDeleteAsync(uploadId, _userId, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task RefuseRemovalBySomebodyWhoNeitherOwnsTheFileNorMayEditThePost()
    {
        var uploadId = Guid.NewGuid();
        StoredAttachment(uploadId);
        _intentionManager
            .Setup(m => m.IsAllowed(UploadIntention.Delete, It.IsAny<StoredUpload>()))
            .Returns(false);
        _intentionManager
            .Setup(m => m.ThrowIfForbidden(UploadIntention.Delete, It.IsAny<StoredUpload>()))
            .Throws(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав для этого действия"));
        var service = Service(new AllowingAuthorizer(UploadType.PostAttachment, mayDetach: false));

        var act = () => service.DeleteUpload(uploadId);

        await act.Should().ThrowAsync<HttpException>();
        _repository.Verify(
            r => r.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
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
            .Setup(m => m.IsAllowed(UploadIntention.Delete, It.IsAny<StoredUpload>()))
            .Returns(true);
        var refusing = new AllowingAuthorizer(UploadType.PostAttachment, mayDetach: false);
        var service = Service(refusing);

        await service.DeleteUpload(uploadId);

        refusing.DetachAsked.Should().BeFalse();
        _repository.Verify(
            r => r.SoftDeleteAsync(uploadId, _userId, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void StoredAttachment(Guid uploadId) =>
        _repository.Setup(r => r.GetAsync(uploadId))
            .ReturnsAsync(new StoredUpload
            {
                Id = uploadId,
                UserId = Guid.NewGuid(),
                Type = UploadType.PostAttachment,
                TargetId = _postId,
                FileName = "karta.png",
                ContentType = "image/png",
                Status = UploadStatus.Confirmed,
            });

    private void VerifyNothingWasProcessedOrStored()
    {
        _imageProcessing.Verify(s => s.ProcessAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<UploadType>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _objectStorage.Verify(s => s.PutAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<NewUpload>()), Times.Never);
    }

    private UploadApiService Service(params IUploadTargetAuthorizer[] authorizers)
    {
        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(Identities.User(_userId));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(p => p.Now).Returns(DateTimeOffset.UtcNow);

        return new UploadApiService(
            _repository.Object,
            identityProvider.Object,
            _intentionManager.Object,
            Mock<IUserService>().Object,
            dateTimeProvider.Object,
            _objectStorage.Object,
            _imageProcessing.Object,
            Mock<ICache>().Object,
            Mock<IHttpContextAccessor>().Object,
            authorizers,
            Options.Create(new CdnConfiguration
            {
                BucketName = "dm-test",
                PublicUrl = "https://cdn.example",
            }));
    }

    /// <summary>
    /// A file of the given length. The bytes are never decoded — the pipeline is
    /// mocked — so only the length matters, and the size guard reads it before
    /// anything looks inside.
    /// </summary>
    private static IFormFile File(long length = 1024)
    {
        var bytes = Encoding.UTF8.GetBytes("not a real image; processing is mocked");
        return new FormFile(new MemoryStream(bytes), 0, length, "file", "karta.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };
    }

    private sealed class AllowingAuthorizer(UploadType type, bool mayDetach = false) : IUploadTargetAuthorizer
    {
        public UploadType Type { get; } = type;

        /// <summary>Whether the removal path consulted this rule at all.</summary>
        public bool DetachAsked { get; private set; }

        public Task EnsureAllowedAsync(Guid targetId) => Task.CompletedTask;

        public Task EnsureReadAllowedAsync(Guid targetId) => Task.CompletedTask;

        public Task<bool> MayDetachAsync(Guid targetId)
        {
            DetachAsked = true;
            return Task.FromResult(mayDetach);
        }
    }
}
