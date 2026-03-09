using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Profiles;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class ModeratedProfileRepository : IModeratedProfileRepository
{
    private readonly DmDbContext _dbContext;

    public ModeratedProfileRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task UpdateUserInfo(string username, string info, CancellationToken ct = default)
    {
        var userEntity = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && !u.IsRemoved, ct);

        if (userEntity == null)
        {
            throw new InvalidOperationException($"User '{username}' not found");
        }

        userEntity.Info = info?.Trim() ?? string.Empty;
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetUserRole(string username, UserRole role, CancellationToken ct = default)
    {
        var userEntity = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && !u.IsRemoved, ct);

        if (userEntity == null)
        {
            throw new InvalidOperationException($"User '{username}' not found");
        }

        userEntity.Role = role;
        await _dbContext.SaveChangesAsync(ct);
    }
}
