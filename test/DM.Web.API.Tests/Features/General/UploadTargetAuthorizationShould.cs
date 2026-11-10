using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Domain.Personal.Features.Profiles;
using DM.Testing;
using DM.Web.API.Features.General.Upload;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// An upload may only point at something the caller is allowed to change.
/// </summary>
/// <remarks>The refusal costs the caller nothing: neither the image pipeline nor the bucket is touched before it.</remarks>
public class UploadTargetAuthorizationShould : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly IObjectStorage _objectStorage;
    private readonly IImageProcessingService _imageProcessing;
    private readonly IUploadRepository _repository;

    public UploadTargetAuthorizationShould()
    {
        _objectStorage = Mock<IObjectStorage>();

        _imageProcessing = Mock<IImageProcessingService>();
        _imageProcessing
            .ProcessAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessedImage(new byte[] { 1, 2, 3 }, "image/png", ".png", 200, 150));

        _repository = Mock<IUploadRepository>();
        _repository.AddAsync(Arg.Any<NewUpload>()).Returns(ci => Uploads.Stored(ci.ArgAt<NewUpload>(0)));
    }

    [Fact]
    public void CoverEveryUploadTypeWithAnAuthorizer()
    {
        var authorizerTypes = new[]
            {
                typeof(IUserService).Assembly,
                typeof(DM.Domain.Game.Authorization.CharacterIntention).Assembly,
            }
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && typeof(IUploadTargetAuthorizer).IsAssignableFrom(t))
            .ToList();

        authorizerTypes.Should().NotBeEmpty("the reflection must find the implementations");

        var covered = authorizerTypes
            .Select(t => (UploadType)t.GetProperty(nameof(IUploadTargetAuthorizer.Type))!
                .GetValue(RuntimeHelpers.GetUninitializedObject(t))!)
            .ToList();

        covered.Should().BeEquivalentTo(Enum.GetValues<UploadType>(),
            "an upload type with no rule cannot be uploaded at all");
        covered.Should().OnlyHaveUniqueItems("two rules for one type is ambiguous");
    }

    [Fact]
    public async Task RefuseATargetTheCallerHasNoRightTo()
    {
        var service = Service(new RefusingAuthorizer(UploadType.CharacterAvatar));

        var act = () => service.DirectUpload(Uploads.File(), UploadType.CharacterAvatar, Guid.NewGuid(), UploadAudience.All);

        await act.Should().ThrowAsync<HttpException>();
        await _imageProcessing.DidNotReceive().ProcessAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>());
        await _objectStorage.DidNotReceive().PutAsync(
                Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<NewUpload>());
    }

    [Fact]
    public async Task FailClosedWhenNoAuthorizerAnswersForTheType()
    {
        var service = Service();

        var act = () => service.DirectUpload(Uploads.File(), UploadType.CharacterAvatar, Guid.NewGuid(), UploadAudience.All);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a type with no rule must not be uploadable");
    }

    private UploadApiService Service(params IUploadTargetAuthorizer[] authorizers) =>
        Uploads.Service(_userId, _repository, _imageProcessing, _objectStorage, Mock<IIntentionManager>(), authorizers);

    private sealed class RefusingAuthorizer : IUploadTargetAuthorizer
    {
        public RefusingAuthorizer(UploadType type) => Type = type;

        public UploadType Type { get; }

        public Task EnsureAllowedAsync(Guid targetId) =>
            throw new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав для этого действия");

        public Task EnsureReadAllowedAsync(StoredUpload upload) =>
            throw new HttpException(HttpStatusCode.NotFound, "Файл не найден");

        public Task<IReadOnlySet<Guid>> WithheldAsync(IReadOnlyCollection<StoredUpload> uploads) =>
            throw new HttpException(HttpStatusCode.NotFound, "Файл не найден");

        public Task<bool> OpenToGuestAsync(StoredUpload upload) => Task.FromResult(false);

        public Task<string?> FreezeAudienceAsync(Guid targetId, Guid? uploadId, UploadAudience audience) =>
            throw new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав для этого действия");

        public Task<bool> MayDetachAsync(Guid targetId) => Task.FromResult(false);
    }
}
