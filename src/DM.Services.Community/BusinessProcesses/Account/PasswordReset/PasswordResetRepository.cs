using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordReset;

/// <inheritdoc />
internal class PasswordResetRepository : IPasswordResetRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public PasswordResetRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task CreateToken(Token token)
    {
        _dbContext.Tokens.Add(token);
        return _dbContext.SaveChangesAsync();
    }
}