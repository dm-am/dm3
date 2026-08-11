using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Tokens;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class TokenMaintenanceRepository : ITokenMaintenanceRepository
{
    private readonly DmDbContext _dbContext;

    public TokenMaintenanceRepository(DmDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    /// <remarks>
    /// IgnoreQueryFilters is what makes the withdrawn half of this reachable.
    /// Token is soft-deletable, so the global filter is already "NOT IsRemoved"
    /// and the predicate composed with it read "NOT IsRemoved AND (IsRemoved OR
    /// older than the cutoff)" — no withdrawn token could ever match, and they
    /// sat in the table until the age clause caught them a week later.
    /// </remarks>
    public Task<int> DeleteWithdrawnOrIssuedBefore(
        DateTimeOffset issuedBefore, CancellationToken cancellationToken = default) =>
        _dbContext.Tokens
            .IgnoreQueryFilters()
            .TagWith("DM.Account.TokenRetention")
            .Where(t => t.IsRemoved || t.CreatedUtc < issuedBefore)
            .ExecuteDeleteAsync(cancellationToken);
}
