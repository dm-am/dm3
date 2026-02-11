using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.Games.Shared;

/// <inheritdoc />
internal class UserRepository : IUserRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public UserRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(bool exists, Guid userId)> FindUserId(string login)
    {
        var foundUserId = await _dbContext.Users
            .Where(u => !u.IsRemoved && u.Login.ToLower() == login.ToLower())
            .Select(u => u.UserId)
            .FirstOrDefaultAsync();
        return (foundUserId != default, foundUserId);
    }

    /// <inheritdoc />
    public Task<bool> UserExists(string login, CancellationToken cancellationToken) =>
        _dbContext.Users.AnyAsync(u =>
            u.Login.ToLower() == login.ToLower() &&
            !u.IsRemoved, cancellationToken);
}