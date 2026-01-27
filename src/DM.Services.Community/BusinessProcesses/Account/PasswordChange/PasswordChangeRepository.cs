using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Authentication.Dto;
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

    /// <inheritdoc />
    public PasswordChangeRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<AuthenticatedUser> FindUser(string login) => _dbContext.Users
        .Where(u => u.Login.ToLower() == login.ToLower())
        .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<AuthenticatedUser> FindUser(Guid tokenId) => _dbContext.Tokens
        .Where(u => u.TokenId == tokenId)
        .Select(u => u.User)
        .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<bool> TokenValid(Guid tokenId, DateTimeOffset createdSince) => _dbContext.Tokens
        .AnyAsync(t => t.TokenId == tokenId &&
                       t.Type == TokenType.PasswordChange && t.CreatedUtc > createdSince);

    /// <inheritdoc />
    public Task UpdatePassword(IUpdateBuilder<User> userUpdate, IUpdateBuilder<Token> tokenUpdate)
    {
        userUpdate.AttachTo(_dbContext);
        tokenUpdate?.AttachTo(_dbContext);
        return _dbContext.SaveChangesAsync();
    }
}