using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
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
    private readonly Mock<IAmazonS3> _s3;
    private readonly Mock<IImageProcessingService> _imageProcessing;
    private readonly Mock<IUploadRepository> _repository;

    public UploadTargetAuthorizationShould()
    {
        _s3 = Mock<IAmazonS3>();
        _s3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        _imageProcessing = Mock<IImageProcessingService>();
        _imageProcessing
            .Setup(s => s.ProcessAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedImage(new byte[] { 1, 2, 3 }, "image/png", ".png"));

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

        _imageProcessing.Verify(s => s.ProcessAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _s3.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<NewUpload>()), Times.Never);
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
        identityProvider.Setup(p => p.Current).Returns(Identities.User(_userId));

        var cache = Mock<ICache>();
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(p => p.Now).Returns(DateTimeOffset.UtcNow);

        return new UploadApiService(
            _repository.Object,
            identityProvider.Object,
            Mock<IIntentionManager>().Object,
            Mock<IUserService>().Object,
            dateTimeProvider.Object,
            _s3.Object,
            _imageProcessing.Object,
            cache.Object,
            Mock<IHttpContextAccessor>().Object,
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
    }

    private sealed class AllowingAuthorizer : IUploadTargetAuthorizer
    {
        public AllowingAuthorizer(UploadType type) => Type = type;

        public UploadType Type { get; }

        public Task EnsureAllowedAsync(Guid targetId) => Task.CompletedTask;
    }
}
