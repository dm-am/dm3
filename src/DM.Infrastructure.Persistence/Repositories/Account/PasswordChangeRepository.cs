using System;
using DM.Domain.Core.Tokens;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class PasswordChangeRepository : IPasswordChangeRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public PasswordChangeRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<AuthenticatedUser?> FindUser(string username) => _dbContext.Users
        .Where(u => u.Username.ToLower() == username.ToLower())
        .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    /// <remarks>
    /// Type, removal and age are checked here rather than left to the caller: the
    /// invariant used to hold only because a validator happened to run first, and
    /// one path — reading token info — called this with no validator at all.
    /// </remarks>
    public Task<AuthenticatedUser?> FindUser(Guid secret, DateTimeOffset createdSince)
    {
        var hash = ConfirmationSecret.Hash(secret);
        return _dbContext.Tokens
            .Where(t => t.SecretHash == hash &&
                        t.Type == TokenType.PasswordChange &&
                        !t.IsRemoved &&
                        t.CreatedUtc > createdSince)
            .Select(t => t.User)
            .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public Task<bool> TokenValid(Guid secret, DateTimeOffset createdSince)
    {
        var hash = ConfirmationSecret.Hash(secret);
        return _dbContext.Tokens
            .AnyAsync(t => t.SecretHash == hash &&
                           t.Type == TokenType.PasswordChange &&
                           !t.IsRemoved &&
                           t.CreatedUtc > createdSince);
    }

    /// <inheritdoc />
    public async Task UpdatePassword(Guid userId, string passwordHash, string salt, Guid? secretToInvalidate)
    {
        // Both writes or neither. Separately, a refusal between them left the new
        // password in place with the link that authorised it still live: whoever
        // holds that mail can set the password again, and nothing here notices.
        //
        // Through the strategy because the API host configures EnableRetryOnFailure,
        // and a retrying strategy refuses a transaction opened by hand. Both writes
        // are set-based, so the change tracker takes no part and a retry needs
        // nothing cleared.
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            await _dbContext.Users
                .Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.PasswordHash, passwordHash)
                    .SetProperty(u => u.Salt, salt)
                    // The version travels with the hash. Leaving it behind produces a row
                    // whose stored version no longer describes its hash - harmless while
                    // there is only one scheme, and unrecoverable the moment there are two.
                    .SetProperty(u => u.PasswordHashVersion, PasswordHashing.CurrentVersion));

            if (secretToInvalidate.HasValue)
            {
                var hash = ConfirmationSecret.Hash(secretToInvalidate.Value);
                await _dbContext.Tokens
                    .Where(t => t.SecretHash == hash)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));
            }

            await transaction.CommitAsync();
        });
    }
}
