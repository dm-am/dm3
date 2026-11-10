using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.General.Upload;
using DM.Web.API.Shared.Dto;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// Who sees a file of a post, as the upload service carries it: chosen with the file and written in
/// its insert, changed by its author alone, and told only to a caller who may open the file.
/// </summary>
public class UploadAudienceShould : UnitTestBase
{
    private const string MarkForVasya =
        "[{\"character\":\"00000000-0000-0000-0000-0000000000e1\",\"name\":\"Вася\"," +
        "\"owner\":\"00000000-0000-0000-0000-0000000000e2\"}]";

    private static readonly UploadAudience ForVasya =
        new(UploadVisibleTo.Selected, [Guid.Parse("00000000-0000-0000-0000-0000000000e1")]);

    /// <summary>The character of <see cref="MarkForVasya"/> as the record names him to its author.</summary>
    private static readonly UploadAddressee Vasya = new() { Id = ForVasya.CharacterIds.Single(), Name = "Вася" };

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();
    private readonly IObjectStorage _objectStorage;
    private readonly IImageProcessingService _imageProcessing;
    private readonly IUploadRepository _repository;
    private readonly IIntentionManager _intentionManager;

    public UploadAudienceShould()
    {
        _objectStorage = Mock<IObjectStorage>();
        _imageProcessing = Mock<IImageProcessingService>();
        _imageProcessing
            .ProcessAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessedImage([1, 2, 3], "image/png", ".png", 16, 16));
        _repository = Mock<IUploadRepository>();
        _repository.AddAsync(Arg.Any<NewUpload>()).Returns(ci => Uploads.Stored(ci.ArgAt<NewUpload>(0)));
        _intentionManager = Mock<IIntentionManager>();
    }

    private UploadApiService Service(params IUploadTargetAuthorizer[] authorizers) =>
        Uploads.Service(_userId, _repository, _imageProcessing, _objectStorage, _intentionManager, authorizers);

    // The mark the module freezes is the one the insert writes: no moment the file is wider.
    [Fact]
    public async Task WriteTheFrozenMarkInTheInsertOfTheFile()
    {
        var authorizer = new MarkingAuthorizer { Mark = MarkForVasya };

        var result = await Service(authorizer)
            .DirectUpload(Uploads.File(), UploadType.PostAttachment, _postId, ForVasya);

        await _repository.Received(1).AddAsync(Arg.Is<NewUpload>(u => u.AddresseesJson == MarkForVasya));
        authorizer.Frozen.Should().ContainSingle().Which.Should().Be((_postId, (Guid?)null, ForVasya));
        result.CanOpen.Should().BeTrue("the uploader reads what he attached");
        result.Recipients.Should().Be("DungeonMaster, Assistant, Вася");
        result.Addressees.Should().BeEquivalentTo(new[] { Vasya });
    }

    // An avatar is everybody's: a chosen audience is refused before the pipeline runs.
    [Fact]
    public async Task RefuseAChosenAudienceForAnAvatarBeforeProcessing()
    {
        var act = () => Service(new MarkingAuthorizer(UploadType.UserAvatar))
            .DirectUpload(Uploads.File(), UploadType.UserAvatar, _userId, ForVasya);

        var refusal = (await act.Should().ThrowAsync<HttpBadRequestException>()).Which;
        refusal.ValidationErrors.Should().ContainKey("visibleTo")
            .WhoseValue.Should().Be(RefusalMessage.AudienceOnlyForPostAttachment);
        await _imageProcessing.DidNotReceive().ProcessAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeTheMarkThroughTheRightToThePost()
    {
        var uploadId = StoredAttachment(addresseesJson: null);
        var authorizer = new MarkingAuthorizer { Mark = MarkForVasya };
        _repository.SetAddressesReturnsTheRow();

        var result = await Service(authorizer).UpdateAudience(uploadId, ForVasya);

        authorizer.AllowedAsked.Should().Equal(_postId);
        authorizer.Frozen.Should().ContainSingle().Which.Should().Be((_postId, (Guid?)uploadId, ForVasya));
        await _repository.Received(1).SetAddresseesAsync(uploadId, MarkForVasya);
        result.Recipients.Should().Be("DungeonMaster, Assistant, Вася");
    }

    [Fact]
    public async Task RefuseToChangeTheMarkForAnybodyButTheAuthor()
    {
        var uploadId = StoredAttachment(addresseesJson: null);
        var authorizer = new MarkingAuthorizer { Refusal = new HttpException(HttpStatusCode.Forbidden, "нельзя") };

        var act = () => Service(authorizer).UpdateAudience(uploadId, UploadAudience.All);

        (await act.Should().ThrowAsync<HttpException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        authorizer.Frozen.Should().BeEmpty();
        await _repository.DidNotReceive().SetAddresseesAsync(Arg.Any<Guid>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseToMarkAnAvatar()
    {
        var uploadId = Guid.NewGuid();
        _repository.GetAsync(uploadId).Returns(new StoredUpload
        {
            Id = uploadId,
            Type = UploadType.UserAvatar,
            TargetId = _userId,
            FileName = "a.png",
        });

        var act = () => Service(new MarkingAuthorizer(UploadType.UserAvatar)).UpdateAudience(uploadId, ForVasya);

        (await act.Should().ThrowAsync<HttpBadRequestException>()).Which.ValidationErrors.Should().ContainKey("visibleTo");
    }

    // A list names a file the post keeps from the caller without opening it, and says nothing of
    // who it is for; a file he may open carries its recipients.
    [Fact]
    public async Task NameAWithheldFileWithoutItsRecipientsAndWithoutOpeningIt()
    {
        var withheld = Row(MarkForVasya);
        var open = Row(MarkForVasya);
        var forEverybody = Row(null);
        _repository.GetPageAsync(Arg.Any<UploadFilter>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns((new[] { withheld, open, forEverybody }, 3));
        var authorizer = new MarkingAuthorizer { Withheld = new HashSet<Guid> { withheld.Id } };

        var (uploads, _) = await Service(authorizer).GetUploads(new UploadsQuery { Take = 10 }, null, all: false);
        var byId = uploads.ToDictionary(u => u.Id);

        byId[withheld.Id].CanOpen.Should().BeFalse();
        byId[withheld.Id].Recipients.Should().BeNull("the names of the addressees are as much the file's as its bytes");
        byId[withheld.Id].Addressees.Should().BeNull();
        byId[open.Id].CanOpen.Should().BeTrue();
        byId[open.Id].Recipients.Should().Be("DungeonMaster, Assistant, Вася");
        byId[open.Id].Addressees.Should().BeEquivalentTo(new[] { Vasya });
        byId[forEverybody.Id].CanOpen.Should().BeTrue();
        byId[forEverybody.Id].Recipients.Should().BeNull("a file for everybody carries no mark");
        byId[forEverybody.Id].Addressees.Should().BeNull();
        authorizer.WithheldAsked.Should().Be(1, "one question for the page");
    }

    private StoredUpload Row(string? addresseesJson) => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        Type = UploadType.PostAttachment,
        TargetId = _postId,
        FileName = "karta.png",
        AddresseesJson = addresseesJson,
    };

    private Guid StoredAttachment(string? addresseesJson)
    {
        var row = Row(addresseesJson);
        _repository.GetAsync(row.Id).Returns(row);
        return row.Id;
    }

    private sealed class MarkingAuthorizer(UploadType type = UploadType.PostAttachment) : IUploadTargetAuthorizer
    {
        public UploadType Type { get; } = type;

        public string? Mark { get; init; }

        public HttpException? Refusal { get; init; }

        public IReadOnlySet<Guid> Withheld { get; init; } = new HashSet<Guid>();

        public List<Guid> AllowedAsked { get; } = [];

        public List<(Guid, Guid?, UploadAudience)> Frozen { get; } = [];

        public int WithheldAsked { get; private set; }

        public Task EnsureAllowedAsync(Guid targetId)
        {
            AllowedAsked.Add(targetId);
            return Refusal == null ? Task.CompletedTask : throw Refusal;
        }

        public Task EnsureReadAllowedAsync(StoredUpload upload) => Task.CompletedTask;

        public Task<IReadOnlySet<Guid>> WithheldAsync(IReadOnlyCollection<StoredUpload> uploads)
        {
            WithheldAsked++;
            return Task.FromResult(Withheld);
        }

        public Task<bool> OpenToGuestAsync(StoredUpload upload) => Task.FromResult(false);

        public Task<string?> FreezeAudienceAsync(Guid targetId, Guid? uploadId, UploadAudience audience)
        {
            Frozen.Add((targetId, uploadId, audience));
            return Task.FromResult(Mark);
        }

        public Task<bool> MayDetachAsync(Guid targetId) => Task.FromResult(false);
    }
}

internal static class UploadRepositoryStubs
{
    public static void SetAddressesReturnsTheRow(this IUploadRepository repository) =>
        repository.SetAddresseesAsync(Arg.Any<Guid>(), Arg.Any<string?>())
            .Returns(ci => new StoredUpload
            {
                Id = ci.ArgAt<Guid>(0),
                Type = UploadType.PostAttachment,
                FileName = "karta.png",
                AddresseesJson = ci.ArgAt<string?>(1),
            });
}
