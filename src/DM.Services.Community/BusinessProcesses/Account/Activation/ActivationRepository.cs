using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.Activation;

/// <summary>
/// Repository for email-first activation flow
/// </summary>
internal class ActivationRepository : IActivationRepository
{
    private readonly DmDbContext _dbContext;

    public ActivationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PendingRegistration?> FindPendingByToken(Guid tokenId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => p.TokenId == tokenId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CompleteActivation(User user, Guid pendingId)
    {
        // Use execution strategy to support NpgsqlRetryingExecutionStrategy with transactions
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Create the user
                _dbContext.Users.Add(user);

                // Delete the pending registration
                var pending = await _dbContext.PendingRegistrations.FindAsync(pendingId);
                if (pending != null)
                {
                    _dbContext.PendingRegistrations.Remove(pending);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<User?> FindUserByEmail(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => EF.Functions.ILike(u.Email, email), cancellationToken);
    }
}
