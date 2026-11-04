using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class EmailLookupRepository : IEmailLookupRepository
{
    private readonly DmDbContext _dbContext;

    public EmailLookupRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<EmailLookupInfo?> GetUserByEmail(string email, CancellationToken ct = default)
    {
        // Past the global filter: a removed account is found and marked as such,
        // so recovery can answer it like a live one and put the truth in the
        // letter instead of on the screen.
        return await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(AccountNames.EmailIs(email.Trim()))
            .Select(u => new EmailLookupInfo
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                IsRemoved = u.IsRemoved
            })
            .FirstOrDefaultAsync(ct);
    }
}
