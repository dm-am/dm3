using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <inheritdoc />
internal class RegistrationRepository : IRegistrationRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public RegistrationRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<bool> EmailFree(string email, CancellationToken cancellationToken) =>
        _dbContext.Users.AllAsync(u => u.Email.ToLower() != email.ToLower(), cancellationToken);

    /// <inheritdoc />
    public Task<bool> LoginFree(string login, CancellationToken cancellationToken) =>
        _dbContext.Users.AllAsync(u => u.Login.ToLower() != login.ToLower(), cancellationToken);

    /// <inheritdoc />
    public Task AddUser(User user, Token token)
    {
        _dbContext.Users.Add(user);
        _dbContext.Tokens.Add(token);
        return _dbContext.SaveChangesAsync();
    }
}