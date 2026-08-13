using System;
using DM.Domain.Core.Tokens;
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
    public async Task<PendingRegistration?> FindPendingByToken(Guid secret, CancellationToken cancellationToken = default)
    {
        var hash = ConfirmationSecret.Hash(secret);
        var entity = await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(p => p.SecretHash == hash, cancellationToken);

        if (entity == null)
            return null;

        return new PendingRegistration
        {
            PendingRegistrationId = entity.PendingRegistrationId,
            SecretHash = entity.SecretHash,
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

        // Both writes or neither: an account created without its pending row
        // removed can be activated a second time, and a pending row removed
        // without its account is a registration that can never be finished and
        // cannot be started again either - the address is taken by a row nobody
        // can log in as.
        //
        // Through the strategy because the API host configures EnableRetryOnFailure,
        // and a transaction opened outside one is retried by nothing. No tracker
        // reset between attempts, unlike the repositories that do reset: dbUser is
        // built above this block, so Add puts the same instance back into Added
        // whatever a failed attempt left behind, and the pending row read below is
        // re-read by FindAsync from the state the rollback restored.
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
            .Where(u => u.Email.ToLower() == email.ToLower())
            .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
