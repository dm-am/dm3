using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// An upload may only point at something the caller is allowed to change.
/// </summary>
/// <remarks>
/// The endpoint used to check only that a target was named. Any authenticated user
/// could POST a CharacterAvatar naming any character, which both replaced that
/// character's portrait and — with a second request — left two live rows pointing
/// at it, and the room read keyed a dictionary on the target and threw. Two
/// requests turned every read of that room into a 500 for everyone in it.
///
/// What is asserted here is the guard and its placement, because the placement is
/// half the value: a refusal must cost the caller nothing, so neither the image
/// pipeline nor the bucket may be touched before the answer is no.
/// </remarks>
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
        _repository.AddAsync(Arg.Any<NewUpload>()).Returns(ci =>
        {
            var u = ci.ArgAt<NewUpload>(0); return new StoredUpload
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
            };
        });
    }

    /// <summary>
    /// Every value of the enum has a rule. A type without one is refused rather
    /// than accepted, but a type reaching production with no rule is a defect
    /// either way, so the coverage is asserted over the shipped assemblies.
    /// </summary>
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
                .GetValue(FormatterServicesCreate(t))!)
            .ToList();

        covered.Should().BeEquivalentTo(Enum.GetValues<UploadType>(),
            "an upload type with no rule cannot be uploaded at all");
        covered.Should().OnlyHaveUniqueItems("two rules for one type is ambiguous");
    }

    [Fact]
    public async Task RefuseATargetTheCallerHasNoRightTo()
    {
        var service = Service(new RefusingAuthorizer(UploadType.CharacterAvatar));

        var act = () => service.DirectUpload(File(), UploadType.CharacterAvatar, Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>();
    }

    /// <summary>
    /// The refusal has to land before the image pipeline and before the bucket.
    /// A PUT that happens anyway leaves an object nothing will ever collect — the
    /// orphan sweeper walks rows, and a refused request writes none.
    /// </summary>
    [Fact]
    public async Task RefuseBeforeProcessingTheImageOrTouchingTheBucket()
    {
        var service = Service(new RefusingAuthorizer(UploadType.CharacterAvatar));

        try
        {
            await service.DirectUpload(File(), UploadType.CharacterAvatar, Guid.NewGuid());
        }
        catch (HttpException)
        {
            // expected
        }

        await _imageProcessing.DidNotReceive().ProcessAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<UploadType>(), Arg.Any<CancellationToken>());
        await _objectStorage.DidNotReceive().PutAsync(
                Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<NewUpload>());
    }

    /// <summary>
    /// A key is never reused and never overwritten, so it has to stay unique for
    /// as long as the bucket lives.
    /// </summary>
    /// <remarks>
    /// Not access control, and the name of this test used to say it was. The
    /// bucket answers anonymously on the avatar prefixes and nowhere else, and an
    /// attachment's bytes come from an endpoint that authorizes the caller — the
    /// length of a key was never what kept a private room private, and reading it
    /// as though it were is how a truncated key gets proposed as a saving.
    /// </remarks>
    [Fact]
    public async Task NameTheObjectWithEnoughRandomnessToNeverCollide()
    {
        var written = new List<NewUpload>();
        _repository.AddAsync(Arg.Do<NewUpload>(written.Add)).Returns(new StoredUpload());

        var service = Service(new AllowingUploadAuthorizer(UploadType.PostAttachment));

        await service.DirectUpload(File(), UploadType.PostAttachment, Guid.NewGuid());

        written.Should().ContainSingle();
        var suffix = written[0].ObjectKey.Split('_')[^1].Split('.')[0];
        // A whole GUID in "N" form: 32 hex characters, not a prefix of one
        suffix.Should().HaveLength(32).And.MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public async Task FailClosedWhenNoAuthorizerAnswersForTheType()
    {
        var service = Service();

        var act = () => service.DirectUpload(File(), UploadType.CharacterAvatar, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a type with no rule must not be uploadable");
    }

    private UploadApiService Service(params IUploadTargetAuthorizer[] authorizers)
    {
        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(_userId));

        var cache = Mock<ICache>();
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        return new UploadApiService(
            _repository,
            identityProvider,
            Mock<IIntentionManager>(),
            Mock<IUserService>(),
            dateTimeProvider,
            _objectStorage,
            _imageProcessing,
            cache,
            Mock<IHttpContextAccessor>(),
            authorizers,
            Options.Create(new CdnConfiguration
            {
                BucketName = "dm-test",
                PublicUrl = "https://cdn.example",
            }));
    }

    private static IFormFile File()
    {
        var bytes = Encoding.UTF8.GetBytes("not a real image; processing is mocked");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "portrait.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };
    }

    /// <summary>
    /// The authorizers take dependencies this test does not want to build; only
    /// the Type property is read, so an uninitialized instance is enough.
    /// </summary>
    private static object FormatterServicesCreate(Type type) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);

    private sealed class RefusingAuthorizer : IUploadTargetAuthorizer
    {
        public RefusingAuthorizer(UploadType type) => Type = type;

        public UploadType Type { get; }

        public Task EnsureAllowedAsync(Guid targetId) =>
            throw new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав для этого действия");

        public Task EnsureReadAllowedAsync(Guid targetId) =>
            throw new HttpException(HttpStatusCode.NotFound, "Файл не найден");

        public Task<bool> MayDetachAsync(Guid targetId) => Task.FromResult(false);
    }
}
