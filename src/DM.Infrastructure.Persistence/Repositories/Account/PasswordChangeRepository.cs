using System;
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
        .Where(u => EF.Functions.ILike(u.Username, username))
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
    public async Task UpdatePassword(Guid userId, string passwordHash, string salt, Guid? tokenIdToInvalidate)
    {
        await _dbContext.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.PasswordHash, passwordHash)
                .SetProperty(u => u.Salt, salt));

        if (tokenIdToInvalidate.HasValue)
        {
            await _dbContext.Tokens
                .Where(t => t.TokenId == tokenIdToInvalidate.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true));
        }
    }
}
