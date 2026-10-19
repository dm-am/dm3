using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Repositories.General;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using IObjectStorage = DM.Domain.Core.Uploads.IObjectStorage;

namespace DM.Infrastructure.Persistence.Tests.Repositories.General;

/// <summary>
/// The orphan sweeper looks for soft-deleted uploads past the global "not removed" filter: a row past the
/// grace period is collected, fresh and live rows are not.
/// </summary>
public class UploadOrphanCollectorShould : UnitTestBase
{
    /// <summary>
    /// The instant every row here is dated from, owned by the test rather than read off the wall clock.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2020, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly IObjectStorage _objectStorage;
    private readonly IDateTimeProvider _clock;

    public UploadOrphanCollectorShould()
    {
        _objectStorage = Mock<IObjectStorage>();
        _objectStorage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _clock = Mock<IDateTimeProvider>();
        _clock.Now.Returns(Now);
    }

    private DmDbContext Context() => InMemoryStore.Open(_databaseName);

    private static Upload NewUpload(bool removed, DateTimeOffset? deletedUtc, string objectKey) => new()
    {
        UploadId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CreatedUtc = Now.AddDays(-10),
        ContentType = "image/png",
        SizeBytes = 1024,
        ObjectKey = objectKey,
        IsRemoved = removed,
        DeletedUtc = deletedUtc,
    };

    private async Task GivenUpload(Upload upload)
    {
        await using var dbContext = Context();
        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>One pass, on a context of its own as a background scope has.</summary>
    private async Task Sweep()
    {
        await using var dbContext = Context();
        await new UploadOrphanCollector(dbContext, _objectStorage, _clock)
            .SweepAsync(CancellationToken.None);
    }

    private async Task<int> RemainingUploads()
    {
        await using var dbContext = Context();
        return await dbContext.Uploads.IgnoreQueryFilters().CountAsync();
    }

    [Fact]
    public async Task CollectUploadRemovedBeyondTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, Now.AddDays(-2), "uploads/expired.png"));

        await Sweep();

        await _objectStorage.Received(1).DeleteAsync("uploads/expired.png", Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(0);
    }

    [Fact]
    public async Task LeaveUploadStillWithinTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, Now.AddHours(-1), "uploads/fresh.png"));

        await Sweep();

        await _objectStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(1);
    }

    [Fact]
    public async Task LeaveUploadThatIsNotDeleted()
    {
        await GivenUpload(NewUpload(removed: false, deletedUtc: null, "uploads/live.png"));

        await Sweep();

        await _objectStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(1);
    }

    /// <summary>
    /// One row is left alone before the deadline and collected after it, with only the clock moving.
    /// </summary>
    [Fact]
    public async Task CollectUploadOnceTheClockPassesTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, Now, "uploads/aging.png"));

        await Sweep();
        (await RemainingUploads()).Should().Be(1);

        _clock.Now.Returns(Now.AddHours(25));
        await Sweep();

        await _objectStorage.Received(1).DeleteAsync("uploads/aging.png", Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(0);
    }

    /// <summary>
    /// The thumbnails of an avatar go with it: no row names them, so nothing would find them later.
    /// </summary>
    [Fact]
    public async Task CollectTheThumbnailsOfAnAvatarWithIt()
    {
        var upload = NewUpload(removed: true, Now.AddDays(-2), "avatars/expired.png");
        upload.Type = UploadType.UserAvatar;
        await GivenUpload(upload);

        await Sweep();

        foreach (var key in new[] { "avatars/expired.png", "avatars/expired_100.webp", "avatars/expired_400.webp" })
        {
            await _objectStorage.Received(1).DeleteAsync(key, Arg.Any<CancellationToken>());
        }
        (await RemainingUploads()).Should().Be(0);
    }

    /// <summary>
    /// A thumbnail the store refused keeps the row for the next pass, as the original does.
    /// </summary>
    [Fact]
    public async Task KeepTheRowOfAnAvatarWhoseThumbnailTheStoreRefused()
    {
        var upload = NewUpload(removed: true, Now.AddDays(-2), "avatars/stuck.png");
        upload.Type = UploadType.UserAvatar;
        await GivenUpload(upload);
        _objectStorage.DeleteAsync("avatars/stuck_400.webp", Arg.Any<CancellationToken>()).Returns(false);

        await Sweep();

        (await RemainingUploads()).Should().Be(1);
    }

    /// <summary>A warning on a profile that pins the upload as the avatar it showed.</summary>
    private async Task GivenPinnedBy(Upload upload)
    {
        await using var dbContext = Context();
        dbContext.Warnings.Add(new Warning
        {
            WarningId = Guid.NewGuid(),
            TargetUserId = upload.UserId,
            AuthorId = Guid.NewGuid(),
            EntityId = upload.UserId,
            EntityType = WarningEntityType.Profile,
            CreatedUtc = Now.AddDays(-12),
            Text = "Неприемлемый аватар",
            ProfileAvatarUploadId = upload.UploadId
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// An avatar a warning pinned outlives its replacement: the open log shows it as the evidence.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeaveAPinnedAvatarItsOwnerTookDown(bool signedByTheOwner)
    {
        var upload = NewUpload(removed: true, Now.AddDays(-10), "avatars/pinned.png");
        upload.DeletedByUserId = signedByTheOwner ? upload.UserId : null;
        await GivenUpload(upload);
        await GivenPinnedBy(upload);

        await Sweep();

        await _objectStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(1);
    }

    /// <summary>
    /// The pin does not keep a picture from moderation: deleted by anybody but
    /// its owner, the pinned avatar is collected as any other.
    /// </summary>
    [Fact]
    public async Task CollectAPinnedAvatarModerationDeleted()
    {
        var upload = NewUpload(removed: true, Now.AddDays(-10), "avatars/forbidden.png");
        upload.DeletedByUserId = Guid.NewGuid();
        await GivenUpload(upload);
        await GivenPinnedBy(upload);

        await Sweep();

        await _objectStorage.Received(1).DeleteAsync("avatars/forbidden.png", Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(0);
    }

    /// <summary>
    /// An avatar nothing pins goes after the grace period.
    /// </summary>
    [Fact]
    public async Task CollectAReplacedAvatarNothingPins()
    {
        await GivenPinnedBy(NewUpload(removed: true, Now.AddDays(-10), "avatars/other.png"));
        await GivenUpload(NewUpload(removed: true, Now.AddDays(-10), "avatars/replaced.png"));

        await Sweep();

        await _objectStorage.Received(1).DeleteAsync("avatars/replaced.png", Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(0);
    }

    /// <summary>
    /// An avatar uploaded and never saved to the profile: the tab was closed
    /// before "Сохранить", and nothing ever marked it deleted.
    /// </summary>
    [Fact]
    public async Task CollectAnAvatarNeverSavedToTheProfile()
    {
        await GivenUpload(UnsavedAvatar(Now.AddDays(-2), "avatars/unsaved.png"));

        await Sweep();

        await _objectStorage.Received(1).DeleteAsync("avatars/unsaved.png", Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(0);
    }

    /// <summary>
    /// Within the grace period the avatar is still on its way to "Сохранить".
    /// </summary>
    [Fact]
    public async Task LeaveAnUnsavedAvatarWithinTheGracePeriod()
    {
        await GivenUpload(UnsavedAvatar(Now.AddHours(-1), "avatars/pending.png"));

        await Sweep();

        await _objectStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(1);
    }

    [Fact]
    public async Task LeaveTheAvatarOfAProfile()
    {
        var avatar = UnsavedAvatar(Now.AddDays(-30), "avatars/current.png");
        await GivenUpload(avatar);
        await using (var dbContext = Context())
        {
            dbContext.Users.Add(new DM.Infrastructure.Persistence.Entities.Account.User
            {
                UserId = avatar.UserId,
                Username = "AvatarOwner",
                Email = "owner@example.com",
                PasswordHash = "hash",
                Salt = "salt",
                AvatarUploadId = avatar.UploadId
            });
            await dbContext.SaveChangesAsync();
        }

        await Sweep();

        await _objectStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(1);
    }

    private static Upload UnsavedAvatar(DateTimeOffset createdUtc, string objectKey)
    {
        var upload = NewUpload(removed: false, deletedUtc: null, objectKey);
        upload.Type = UploadType.UserAvatar;
        upload.Status = UploadStatus.Confirmed;
        upload.CreatedUtc = createdUtc;
        return upload;
    }
}
