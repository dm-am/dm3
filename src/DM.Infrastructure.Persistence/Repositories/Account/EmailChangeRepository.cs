using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Tokens;
using Microsoft.EntityFrameworkCore;
using TokenEntity = DM.Infrastructure.Persistence.Entities.Account.Token;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class EmailChangeRepository : IEmailChangeRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public EmailChangeRepository(
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
    public async Task<bool> IsEmailFree(string email, CancellationToken ct) =>
        !await _dbContext.Users.AnyAsync(u => EF.Functions.ILike(u.Email, email) && !u.IsRemoved, ct);

    /// <inheritdoc />
    public async Task Update(Guid userId, string newEmail, CreateToken tokenDto)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) return;

        user.Email = newEmail;

        var tokenEntity = new TokenEntity
        {
            TokenId = tokenDto.TokenId,
            UserId = tokenDto.UserId,
            EntityId = tokenDto.EntityId,
            CreatedUtc = tokenDto.CreatedUtc,
            Type = tokenDto.Type,
            CreatorId = tokenDto.CreatorId,
            IsRemoved = false
        };
        _dbContext.Tokens.Add(tokenEntity);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task InvalidateOldEmailChangeTokens(Guid userId)
    {
        var oldTokens = await _dbContext.Tokens
            .Where(t => t.UserId == userId && t.Type == TokenType.EmailChange && !t.IsRemoved)
            .ToListAsync();

        foreach (var token in oldTokens)
        {
            token.IsRemoved = true;
        }

        if (oldTokens.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
