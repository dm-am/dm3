using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Web.API.Services.Dev;

/// <inheritdoc />
internal class DevApiService : IDevApiService
{
    private readonly DmDbContext _dbContext;
    private readonly IIdentityProvider _identityProvider;

    private const string DefaultPassword = "Test123!";

    /// <summary>
    /// Creates a new instance of <see cref="DevApiService"/>
    /// </summary>
    public DevApiService(
        DmDbContext dbContext,
        IIdentityProvider identityProvider)
    {
        _dbContext = dbContext;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task SetRole(UserRole role)
    {
        var userId = _identityProvider.Current.User.UserId;
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user != null)
        {
            user.Role = role;
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestAccountInfo>> GetAllUsers()
    {
        var users = await _dbContext.Users
            .Where(u => !u.IsRemoved)
            .OrderByDescending(u => u.Role)
            .ThenBy(u => u.Login)
            .Select(u => new TestAccountInfo
            {
                Login = u.Login,
                Password = DefaultPassword,
                Role = u.Role
            })
            .ToListAsync();

        return users;
    }
}
