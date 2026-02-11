using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Authentication.Dto;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange;

/// <inheritdoc />
internal class EmailChangeRepository : IEmailChangeRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public EmailChangeRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<AuthenticatedUser?> FindUser(string login) => _dbContext.Users
        .Where(u => EF.Functions.ILike(u.Login, login))
        .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<bool> IsEmailFree(string email, CancellationToken ct) =>
        !await _dbContext.Users.AnyAsync(u => EF.Functions.ILike(u.Email, email) && !u.IsRemoved, ct);

    /// <inheritdoc />
    public Task Update(IUpdateBuilder<User> updateUser, Token token)
    {
        updateUser.AttachTo(_dbContext);
        _dbContext.Tokens.Add(token);
        return _dbContext.SaveChangesAsync();
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