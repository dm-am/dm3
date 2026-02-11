using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordChange;

/// <inheritdoc />
internal class PasswordChangeRepository : IPasswordChangeRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ISecurityManager _securityManager;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PasswordChangeRepository(
        DmDbContext dbContext,
        IMapper mapper,
        ISecurityManager securityManager,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _securityManager = securityManager;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<AuthenticatedUser?> FindUser(string login) => _dbContext.Users
        .Where(u => EF.Functions.ILike(u.Login, login))
        .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<AuthenticatedUser?> FindUser(Guid tokenId) => _dbContext.Tokens
        .Where(u => u.TokenId == tokenId)
        .Select(u => u.User)
        .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<bool> TokenValid(Guid tokenId, DateTimeOffset createdSince) => _dbContext.Tokens
        .AnyAsync(t => t.TokenId == tokenId &&
                       t.Type == TokenType.PasswordChange &&
                       !t.IsRemoved &&
                       t.CreatedUtc > createdSince);

    /// <inheritdoc />
    public Task UpdatePassword(IUpdateBuilder<User> userUpdate, IUpdateBuilder<Token>? tokenUpdate)
    {
        userUpdate.AttachTo(_dbContext);
        tokenUpdate?.AttachTo(_dbContext);
        return _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<bool> IsPasswordReused(Guid userId, string newPassword, int checkCount, CancellationToken ct)
    {
        // Get last N password history entries
        var recentPasswords = await _dbContext.PasswordHistories
            .Where(ph => ph.UserId == userId)
            .OrderByDescending(ph => ph.CreatedUtc)
            .Take(checkCount)
            .Select(ph => new { ph.PasswordHash, ph.Salt, ph.PasswordHashVersion })
            .ToListAsync(ct);

        // Check if any of the recent passwords match the new password
        foreach (var historyEntry in recentPasswords)
        {
            if (_securityManager.ComparePasswords(newPassword, historyEntry.Salt, historyEntry.PasswordHash, historyEntry.PasswordHashVersion))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public Task SavePasswordToHistory(Guid userId, string passwordHash, string salt, int version, CancellationToken ct)
    {
        var historyEntry = new PasswordHistory
        {
            PasswordHistoryId = _guidFactory.Create(),
            UserId = userId,
            PasswordHash = passwordHash,
            Salt = salt,
            PasswordHashVersion = version,
            CreatedUtc = _dateTimeProvider.Now
        };

        _dbContext.PasswordHistories.Add(historyEntry);
        return _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CleanupOldEntries(Guid userId, int keepCount, CancellationToken ct)
    {
        // Get IDs of entries to delete (all except the most recent keepCount)
        var entriesToDelete = await _dbContext.PasswordHistories
            .Where(ph => ph.UserId == userId)
            .OrderByDescending(ph => ph.CreatedUtc)
            .Skip(keepCount)
            .Select(ph => ph.PasswordHistoryId)
            .ToListAsync(ct);

        if (entriesToDelete.Any())
        {
            await _dbContext.PasswordHistories
                .Where(ph => entriesToDelete.Contains(ph.PasswordHistoryId))
                .ExecuteDeleteAsync(ct);
        }
    }
}