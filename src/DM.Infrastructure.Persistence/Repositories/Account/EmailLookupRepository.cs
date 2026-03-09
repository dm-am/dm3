using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
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
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _dbContext.Users
            .Where(u => !u.IsRemoved && u.Email != null && EF.Functions.ILike(u.Email, normalizedEmail))
            .Select(u => new EmailLookupInfo
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email!
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> EmailExists(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _dbContext.Users
            .AnyAsync(u => !u.IsRemoved && u.Email != null && EF.Functions.ILike(u.Email, normalizedEmail), ct);
    }
}
