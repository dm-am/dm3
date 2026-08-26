using System;
using System.Linq;
using System.Threading.Tasks;
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

/// <summary>
/// The intrinsic size of an upload survives the round trip through the database
/// and reaches the projection every reader of an avatar goes through.
/// </summary>
/// <remarks>
/// The profile page draws the source file, which keeps the aspect ratio of
/// whatever was uploaded. Nothing on the wire said what that ratio was, so the
/// page declared a square and held it as a floor: a landscape avatar was drawn
/// 165 px tall inside a 220 px reservation and left the difference blank. The
/// browser reserves the exact box on its own once the img element carries the
/// real pair, and this is what says the pair exists as far down as the schema —
/// against a real Postgres, because the columns and the SQL translation of the
/// projection are the two things a mock cannot vouch for.
///
/// The rows are owned by the seeded test user rather than by users of this
/// test's own: the fixture database is shared and the community listing is
/// asserted over a capped page, so a suite that invents a user per check
/// eventually changes the answer of a test that never mentions uploads.
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

    /// <summary>
    /// A row written without the measurement projects nothing rather than a
    /// default, which is what leaves a reader able to tell "nobody measured this"
    /// from "this picture is zero pixels wide".
    /// </summary>
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

    /// <summary>
    /// The projection as production runs it: an EF Select translated to SQL, so a
    /// column missing from the schema fails here instead of in a later release.
    /// </summary>
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
