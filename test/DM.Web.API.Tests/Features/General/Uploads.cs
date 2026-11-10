using System;
using System.IO;
using System.Text;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Domain.Personal.Features.Profiles;
using DM.Testing.Dsl;
using DM.Web.API.Features.General.Upload;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace DM.Web.API.Tests.Features.General;

internal static class Uploads
{
    public static UploadApiService Service(
        Guid userId,
        IUploadRepository repository,
        IImageProcessingService imageProcessing,
        IObjectStorage objectStorage,
        IIntentionManager intentionManager,
        params IUploadTargetAuthorizer[] authorizers)
    {
        var identityProvider = Substitute.For<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(userId));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        return new UploadApiService(
            repository,
            identityProvider,
            intentionManager,
            Substitute.For<IUserService>(),
            dateTimeProvider,
            objectStorage,
            imageProcessing,
            Substitute.For<ICache>(),
            Substitute.For<IHttpContextAccessor>(),
            authorizers);
    }

    public static StoredUpload Stored(NewUpload upload) => new()
    {
        Id = upload.Id,
        UserId = upload.UserId,
        Type = upload.Type,
        TargetId = upload.TargetId,
        FileName = upload.FileName,
        ContentType = upload.ContentType,
        SizeBytes = upload.SizeBytes,
        Status = upload.Status,
        Url = upload.Url,
        CreatedUtc = upload.CreatedUtc,
        ConfirmedUtc = upload.ConfirmedUtc,
        AddresseesJson = upload.AddresseesJson,
    };

    // A file of the given length; the pipeline is mocked, so only the length matters.
    public static IFormFile File(long length = 1024)
    {
        var bytes = Encoding.UTF8.GetBytes("not a real image; processing is mocked");
        return new FormFile(new MemoryStream(bytes), 0, length, "file", "karta.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };
    }
}
