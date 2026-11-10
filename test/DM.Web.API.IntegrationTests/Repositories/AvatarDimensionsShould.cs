using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Shared.Users;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <remarks>
/// Against a real Postgres: the columns and the projection's SQL translation are what a mock cannot vouch for.
/// The rows belong to the seeded test user, so the shared listing's capped page does not change.
/// </remarks>
public class AvatarDimensionsShould : IntegrationTestBase
{
    public AvatarDimensionsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ReachTheProjectionThatEveryReaderOfAnAvatarGoesThrough()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var uploads = scope.ServiceProvider.GetRequiredService<IUploadRepository>();

        var stored = await uploads.AddAsync(NewAvatar(width: 200, height: 150));

        var picture = await ProjectAsync(dbContext, stored.Id);

        picture.SourceWidth.Should().Be(200);
        picture.SourceHeight.Should().Be(150);
    }

    // A row without the measurement projects nothing, so "nobody measured" differs from "zero
    // pixels".
    [Fact]
    public async Task StayAbsentForAnUploadNobodyMeasured()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var uploads = scope.ServiceProvider.GetRequiredService<IUploadRepository>();

        var stored = await uploads.AddAsync(NewAvatar(width: null, height: null));

        var picture = await ProjectAsync(dbContext, stored.Id);

        picture.SourceUrl.Should().NotBeNull("the file itself is still there");
        picture.SourceWidth.Should().BeNull();
        picture.SourceHeight.Should().BeNull();
    }

    // The projection as production runs it: an EF Select translated to SQL, so a column missing
    // from the schema fails here instead of in a later release.
    private static async Task<AvatarPicture> ProjectAsync(DmDbContext dbContext, Guid uploadId) =>
        await dbContext.Uploads
            .Where(u => u.UploadId == uploadId)
            .Select(u => AvatarProjections.From(u))
            .SingleAsync();

    private static NewUpload NewAvatar(int? width, int? height)
    {
        var objectKey = $"avatars/{Guid.NewGuid():N}.png";
        return new NewUpload
        {
            Id = Guid.NewGuid(),
            UserId = TestConstants.TestUserId,
            TargetId = TestConstants.TestUserId,
            Type = UploadType.UserAvatar,
            Status = UploadStatus.Confirmed,
            FileName = "avatar.png",
            ContentType = "image/png",
            SizeBytes = 1024,
            Width = width,
            Height = height,
            ObjectKey = objectKey,
            Original = true,
            Url = $"https://cdn.example/{objectKey}",
            CreatedUtc = DateTimeOffset.UtcNow,
            ConfirmedUtc = DateTimeOffset.UtcNow,
        };
    }
}
