using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Repositories.General;
using DM.Testing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using IObjectStorage = DM.Domain.Core.Uploads.IObjectStorage;

namespace DM.Infrastructure.Persistence.Tests.Repositories.General;

/// <summary>
/// "Everything but the newest upload of this target is obsolete" is true of a
/// single-slot target and false of a post: attachments are the whole point of
/// the third upload type, and a rule that keeps one would delete the rest.
/// </summary>
/// <remarks>
/// The collector used to take an identifier alone and match it against all three
/// target columns, so the first call naming a post would have swept that post's
/// attachments down to the newest one. It is called with users today, which made
/// the defect a loaded mine rather than a failure — the type is now part of the
/// question, and a multi-slot type is refused instead of being served wrongly.
///
/// The second fact here is about time: the row is soft-deleted and the object it
/// names stays in the bucket, because the sweeper's grace period is what makes
/// "changed my mind within a day" work, and destroying the object immediately
/// left the row promising a file that was already gone.
/// </remarks>
public class UploadGarbageCollectorShould : UnitTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(_databaseName)
        .Options);

    private UploadGarbageCollector Collector(DmDbContext context)
    {
        var clock = Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(Now);
        return new UploadGarbageCollector(context, clock.Object);
    }

    private static Upload NewUpload(UploadType type, Guid target, DateTimeOffset createdUtc) => new()
    {
        UploadId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Type = type,
        CreatedUtc = createdUtc,
        ContentType = "image/png",
        SizeBytes = 1024,
        ObjectKey = $"objects/{Guid.NewGuid():N}.png",
        TargetUserId = type == UploadType.UserAvatar ? target : null,
        TargetCharacterId = type == UploadType.CharacterAvatar ? target : null,
        TargetPostId = type == UploadType.PostAttachment ? target : null,
    };

    private async Task Given(params Upload[] uploads)
    {
        await using var context = Context();
        context.Uploads.AddRange(uploads);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task RetireEveryAvatarOfTheTargetButTheNewest()
    {
        var userId = Guid.NewGuid();
        var previous = NewUpload(UploadType.UserAvatar, userId, Now.AddDays(-2));
        var current = NewUpload(UploadType.UserAvatar, userId, Now.AddMinutes(-1));
        await Given(previous, current);

        await using (var context = Context())
        {
            await Collector(context).CollectObsoleteAsync(userId, UploadType.UserAvatar);
        }

        await using var probe = Context();
        var stored = await probe.Uploads.IgnoreQueryFilters().ToListAsync();
        stored.Single(u => u.UploadId == current.UploadId).IsRemoved.Should().BeFalse();
        var retired = stored.Single(u => u.UploadId == previous.UploadId);
        retired.IsRemoved.Should().BeTrue();
        retired.DeletedUtc.Should().Be(Now,
            "the grace period is measured against the same clock the sweeper reads");
    }

    /// <summary>
    /// A post keeps every attachment its author added, so the type is refused
    /// rather than served with the single-slot rule.
    /// </summary>
    [Fact]
    public async Task RefuseATypeThatHoldsMoreThanOneUploadPerTarget()
    {
        var postId = Guid.NewGuid();
        var first = NewUpload(UploadType.PostAttachment, postId, Now.AddHours(-2));
        var second = NewUpload(UploadType.PostAttachment, postId, Now.AddHours(-1));
        await Given(first, second);

        await using var context = Context();
        var act = () => Collector(context).CollectObsoleteAsync(postId, UploadType.PostAttachment);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await using var probe = Context();
        (await probe.Uploads.IgnoreQueryFilters().CountAsync(u => u.IsRemoved))
            .Should().Be(0, "no attachment of a post is obsolete because another one is newer");
    }

    /// <summary>
    /// Uploads of another type pointing at the same identifier are none of this
    /// call's business: the type is part of the question, not decoration on it.
    /// </summary>
    [Fact]
    public async Task LeaveUploadsOfAnotherTypeAlone()
    {
        var entityId = Guid.NewGuid();
        var avatar = NewUpload(UploadType.UserAvatar, entityId, Now.AddDays(-1));
        var portrait = NewUpload(UploadType.CharacterAvatar, entityId, Now.AddDays(-3));
        await Given(avatar, portrait);

        await using (var context = Context())
        {
            await Collector(context).CollectObsoleteAsync(entityId, UploadType.UserAvatar);
        }

        await using var probe = Context();
        (await probe.Uploads.IgnoreQueryFilters().CountAsync(u => u.IsRemoved))
            .Should().Be(0, "one avatar of each type is one live upload per slot");
    }

    /// <summary>
    /// Retiring a row must not destroy the object it names. The collector holds no
    /// object store at all, which is the mechanical form of that promise: the only
    /// thing that reaches the bucket is the sweeper, after the grace period.
    /// </summary>
    [Fact]
    public void TakeNoObjectStore()
    {
        typeof(UploadGarbageCollector)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Should().NotContain(typeof(IObjectStorage),
                "the object outlives the row by the grace period the sweeper owns");
    }
}
