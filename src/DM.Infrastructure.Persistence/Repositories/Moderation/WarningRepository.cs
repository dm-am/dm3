using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Moderation.Features.Warnings;
using Microsoft.EntityFrameworkCore;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class WarningRepository : IWarningRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public WarningRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Warning>> GetUserWarnings(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Warnings
            .Where(w => w.TargetUserId == userId)
            .OrderByDescending(w => w.CreatedUtc)
            .ProjectToWarning()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Warning> Items, int Total)> GetLog(
        Guid? targetUserId, PagingQuery paging, CancellationToken ct = default)
    {
        IQueryable<DbWarning> query = _dbContext.Warnings;
        if (targetUserId.HasValue)
        {
            query = query.Where(w => w.TargetUserId == targetUserId.Value);
        }

        // Count and page as two reads without a transaction, the way the ban
        // history is read: a warning written between them leaves the total one
        // smaller than the log holds until the next read, and a log counts no
        // money.
        var total = await query.CountAsync(ct);
        var items = await query
            // The identifier breaks the tie. Ordered by the moment alone, two
            // warnings written in the same millisecond may swap places between
            // two reads, so one of them shows up on both pages and the other on
            // neither; equal timestamps are an invariant the DM2 import brings.
            .OrderByDescending(w => w.CreatedUtc)
            .ThenByDescending(w => w.WarningId)
            .Skip(paging.Skip)
            .Take(paging.Take)
            .ProjectToWarning()
            .ToListAsync(ct);

        await AttachPinnedPictures(items, ct);
        return (items, total);
    }

    /// <inheritdoc />
    public async Task<Warning?> Get(Guid warningId, CancellationToken ct = default)
    {
        var warning = await _dbContext.Warnings
            .Where(w => w.WarningId == warningId)
            .ProjectToWarning()
            .FirstOrDefaultAsync(ct);

        if (warning != null)
        {
            await AttachPinnedPictures([warning], ct);
        }

        return warning;
    }

    /// <summary>
    /// Give each warning on a profile the avatar it pinned, unless moderation has
    /// deleted the picture since.
    /// </summary>
    /// <remarks>
    /// The pinned upload is the avatar of the moment of issue, and the warned user
    /// may have replaced it the same day: replacing an avatar marks the previous
    /// upload deleted, with no author. That row is behind the soft-delete filter,
    /// so it is read here with the filter lifted and the deletion stated by hand,
    /// the rule the orphan sweeper keeps by (DATA_STORAGE.md, "Объектное
    /// хранилище"): a deletion with no author or by the owner leaves the picture
    /// to the warning, a deletion by anybody else - moderation in "Все загруженные
    /// файлы" or under the warning itself - takes it out of the answer at once,
    /// before the sweeper gets to the file. Lifting every filter is safe in this
    /// query: it reads the upload and the identifier of the warning, no user.
    /// One read for the page, and none for a page without such a warning.
    /// </remarks>
    private async Task AttachPinnedPictures(IReadOnlyList<Warning> warnings, CancellationToken ct)
    {
        var profiles = warnings
            .Where(w => w.Profile != null)
            .ToDictionary(w => w.WarningId, w => w.Profile!);
        if (profiles.Count == 0)
        {
            return;
        }

        var ids = profiles.Keys.ToList();
        var pictures = await _dbContext.Warnings
            .IgnoreQueryFilters()
            .Where(w => ids.Contains(w.WarningId) && w.ProfileAvatarUpload != null)
            .Where(w => !w.ProfileAvatarUpload!.IsRemoved ||
                        w.ProfileAvatarUpload.DeletedByUserId == null ||
                        w.ProfileAvatarUpload.DeletedByUserId == w.ProfileAvatarUpload.UserId)
            .SelectSpliced(w => new
            {
                w.WarningId,
                Picture = AvatarProjections.Projection.Splice(w.ProfileAvatarUpload)
            })
            .ToListAsync(ct);

        foreach (var pinned in pictures)
        {
            profiles[pinned.WarningId].Picture = pinned.Picture;
        }
    }

    /// <inheritdoc />
    public async Task<Warning> Create(CreateWarningEntity entity, CancellationToken ct = default)
    {
        var warning = new DbWarning
        {
            WarningId = entity.WarningId,
            TargetUserId = entity.TargetUserId,
            AuthorId = entity.AuthorId,
            EntityId = entity.EntityId,
            EntityType = entity.EntityType,
            Points = entity.Points,
            // The points the warning was issued with are written twice on
            // purpose: one of the two columns is the current verdict and moves,
            // the other is what the moderator decided and never does.
            OriginalPoints = entity.Points,
            Text = entity.Text,
            EntitySnapshot = entity.EntitySnapshot,
            ProfileStatusSnapshot = entity.ProfileStatusSnapshot,
            ProfileAvatarUploadId = entity.ProfileAvatarUploadId,
            CreatedUtc = entity.CreatedUtc
        };

        _dbContext.Warnings.Add(warning);
        await _dbContext.SaveChangesAsync(ct);

        return await Get(warning.WarningId, ct) ?? throw new InvalidOperationException("Failed to retrieve created warning");
    }

    /// <inheritdoc />
    public async Task<bool> ChangePoints(Guid warningId, int points, Guid changedByUserId,
        DateTimeOffset changedUtc, string text, CancellationToken ct = default)
    {
        // One statement with the identifier in its WHERE, rather than read,
        // decide and save: two moderators correcting the same warning at once
        // both pass a read, and the tracked write would then put the first
        // signature under the second verdict. The row count answers whether the
        // warning was there at all.
        var changed = await _dbContext.Warnings
            .Where(w => w.WarningId == warningId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(w => w.Points, points)
                .SetProperty(w => w.PointsChangedByUserId, changedByUserId)
                .SetProperty(w => w.PointsChangedUtc, changedUtc)
                .SetProperty(w => w.PointsChangeText, text), ct);

        return changed > 0;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetTargetUserId(Guid warningId, CancellationToken ct = default)
    {
        // The warning row alone, with no join onto the accounts: the question is
        // whether the warning is there and whom it names, which a removed account
        // does not change.
        return await _dbContext.Warnings
            .Where(w => w.WarningId == warningId)
            .Select(w => (Guid?)w.TargetUserId)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProfilePicture(Guid warningId, Guid deletedByUserId,
        DateTimeOffset deletedUtc, CancellationToken ct = default)
    {
        var warning = await _dbContext.Warnings
            .Where(w => w.WarningId == warningId)
            .Select(w => new { w.ProfileAvatarUploadId })
            .FirstOrDefaultAsync(ct);
        if (warning == null)
        {
            return false;
        }

        if (warning.ProfileAvatarUploadId is not { } uploadId)
        {
            return true;
        }

        // Past the soft-delete filter: the pinned avatar is usually one its owner
        // has replaced, and replacing it marked the row deleted with no author.
        // One statement with the rule in its WHERE (DATA_STORAGE.md, "Атомарные
        // встречные записи"), rather than read, decide and mark: two moderators
        // pressing at once both pass a read, and the second write would then move
        // the author and restart the grace period of a deletion that was already
        // moderation's. The second statement waits on the row lock, re-tests the
        // predicate against the committed mark and changes nothing. The predicate
        // is the one the sweeper and the log read the pin by: no author, or the
        // owner, is the owner's own doing and does not count as moderation.
        await _dbContext.Uploads
            .IgnoreQueryFilters()
            .Where(u => u.UploadId == uploadId &&
                        (u.DeletedByUserId == null || u.DeletedByUserId == u.UserId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsRemoved, true)
                .SetProperty(u => u.DeletedByUserId, (Guid?)deletedByUserId)
                .SetProperty(u => u.DeletedUtc, (DateTimeOffset?)deletedUtc), ct);

        return true;
    }

    /// <inheritdoc />
    public async Task<int> GetUserWarningPoints(Guid userId, CancellationToken ct = default)
    {
        // Summed over the current points, so a warning corrected down to zero
        // stops weighing on the scale while staying in the log.
        return await _dbContext.Warnings
            .Where(w => w.TargetUserId == userId)
            .SumAsync(w => w.Points, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserWarningSummary>> GetActiveWarningSummaries(CancellationToken ct = default)
    {
        var aggregates = await _dbContext.Warnings
            .GroupBy(w => w.TargetUserId)
            .Select(g => new
            {
                UserId = g.Key,
                Points = g.Sum(w => w.Points),
                LastWarningUtc = g.Max(w => w.CreatedUtc)
            })
            // Users with only verbal (0-point) warnings are not violators, and
            // neither are those whose every warning has been corrected to zero
            .Where(a => a.Points > 0)
            .ToListAsync(ct);

        if (aggregates.Count == 0)
        {
            return [];
        }

        var userIds = aggregates.Select(a => a.UserId).ToList();
        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.UserId))
            .ProjectToGeneralUser()
            .ToDictionaryAsync(u => u.UserId, ct);

        return aggregates
            .Where(a => users.ContainsKey(a.UserId))
            .Select(a => new UserWarningSummary
            {
                User = users[a.UserId],
                Points = a.Points,
                LastWarningUtc = a.LastWarningUtc
            })
            .ToList();
    }
}
