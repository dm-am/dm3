using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Identity;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <summary>
/// Repository for email-first activation flow
/// </summary>
internal class ActivationRepository : IActivationRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public ActivationRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PendingRegistration?> FindPendingByToken(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => p.TokenId == tokenId, cancellationToken);

        if (entity == null)
            return null;

        return new PendingRegistration
        {
            PendingRegistrationId = entity.PendingRegistrationId,
            TokenId = entity.TokenId,
            Email = entity.Email,
            PasswordHash = entity.PasswordHash,
            Salt = entity.Salt,
            PasswordHashVersion = entity.PasswordHashVersion,
            CreatedUtc = entity.CreatedUtc,
            TokenCreatedUtc = entity.TokenCreatedUtc,
            AcceptedRules = entity.AcceptedRules
        };
    }

    /// <inheritdoc />
    public async Task CompleteActivation(CreateUser user, Guid pendingId)
    {
        var dbUser = new DbUser
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Salt = user.Salt,
            PasswordHashVersion = user.PasswordHashVersion,
            CreatedUtc = user.CreatedUtc,
            Role = user.Role,
            AccessPolicy = user.AccessPolicy,
            LastActivityUtc = user.CreatedUtc
        };

        // Use execution strategy to support NpgsqlRetryingExecutionStrategy with transactions
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Create the user
                _dbContext.Users.Add(dbUser);

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
    public async Task<AuthenticatedUser?> FindUserByEmail(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Where(u => EF.Functions.ILike(u.Email, email))
            .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
