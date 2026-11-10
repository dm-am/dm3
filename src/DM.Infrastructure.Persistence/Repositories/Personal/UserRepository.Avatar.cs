using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

internal partial class UserRepository
{
    /// <inheritdoc />
    public Task<Guid?> GetConfirmedAvatarUpload(Guid userId, Guid uploadId) =>
        _dbContext.Uploads
            .Where(u => u.UploadId == uploadId &&
                        u.UserId == userId &&
                        u.Status == UploadStatus.Confirmed)
            .Select(u => (Guid?)u.UploadId)
            .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task LinkAvatarUpload(Guid userId, Guid uploadId)
    {
        var oldUploads = await _dbContext.Uploads
            .Where(u => u.UserId == userId &&
                        u.UploadId != uploadId &&
                        u.Type == UploadType.UserAvatar)
            .ToListAsync();

        foreach (var oldUpload in oldUploads)
        {
            // No author, and the moment starts the sweeper's grace period; see
            // UnlinkAvatarUpload below.
            SoftDelete.Mark(oldUpload, null, _dateTimeProvider.Now);
        }

        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.AvatarUploadId = uploadId;
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UnlinkAvatarUpload(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) return;
        if (user.AvatarUploadId == null) return; // Idempotent: no avatar already

        user.AvatarUploadId = null;

        // The objects stay for the sweeper's grace period.
        var avatarUploads = await _dbContext.Uploads
            .Where(u => u.UserId == userId && u.Type == UploadType.UserAvatar)
            .ToListAsync();

        foreach (var upload in avatarUploads)
        {
            SoftDelete.Mark(upload, null, _dateTimeProvider.Now);
        }

        await _dbContext.SaveChangesAsync();
    }
}
