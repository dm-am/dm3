using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Deactivation;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <summary>
/// Repository for account deactivation operations
/// </summary>
internal class DeactivationRepository : IDeactivationRepository
{
    private readonly DmDbContext _dbContext;

    public DeactivationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<UserCredentials?> GetUserCredentials(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId && !u.IsRemoved)
            .Select(u => new UserCredentials
            {
                UserId = u.UserId,
                PasswordHash = u.PasswordHash,
                Salt = u.Salt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeactivateUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsRemoved, true), cancellationToken);
    }
}
