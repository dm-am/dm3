using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Profiles;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

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
        var userEntity = await FindLiveUser(username, ct);
        userEntity.Info = AuthoredText.AsTyped(info) ?? string.Empty;
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetUserRole(string username, UserRole role, CancellationToken ct = default)
    {
        var userEntity = await FindLiveUser(username, ct);
        userEntity.Role = role;
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetModerationWatch(string username, bool underWatch, CancellationToken ct = default)
    {
        var userEntity = await FindLiveUser(username, ct);
        userEntity.IsUnderModerationWatch = underWatch;
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<DbUser> FindLiveUser(string username, CancellationToken ct) =>
        await _dbContext.Users
            .Where(AccountNames.UsernameIs(username))
            .FirstOrDefaultAsync(u => !u.IsRemoved, ct)
        ?? throw new InvalidOperationException($"User '{username}' not found");
}
