using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Repositories.Account;

internal class DeactivationRepository : IDeactivationRepository
{
    private readonly DmDbContext _dbContext;

    public DeactivationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // The removed account as moderation reads it, spliced into every read below so the SELECT stays
    // the six columns the screen shows.
    private static readonly Expression<Func<DbUser, RemovedAccountRow>> Projection = user => new RemovedAccountRow(
        user.UserId,
        user.Username,
        user.Email,
        user.Role,
        user.CreatedUtc,
        user.LastActivityUtc);

    /// <inheritdoc />
    public async Task DeactivateUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsRemoved, true), cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The mirror of the removal write above, and the only place in the product
    /// that clears the flag. The row never went anywhere, so the name, the address
    /// and everything written under the name come back with it. Past the global
    /// filter on purpose: every account this is called for is a removed one, and
    /// under the filter the update would match nothing.
    /// </remarks>
    public Task Restore(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => user.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.IsRemoved, false), cancellationToken);

    /// <inheritdoc />
    public Task<RemovedAccountRow?> GetRemovedByUsername(string username,
        CancellationToken cancellationToken = default)
    {
        return Removed(null)
            .Where(AccountNames.UsernameIs(username))
            .Select(Projection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountRemoved(string? search, CancellationToken cancellationToken = default) =>
        Removed(search).CountAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RemovedAccountRow>> GetRemoved(string? search, PagingData paging,
        CancellationToken cancellationToken = default) =>
        await Removed(search)
            .OrderBy(user => user.Username)
            .Page(paging)
            // Projected after the filter and the page, never before: a Where over
            // a member of the projected record is not something the provider can
            // translate back into a column.
            .Select(Projection)
            .ToListAsync(cancellationToken);

    /// <remarks>
    /// Two things happen here at once. The global filter is lifted, because it is
    /// what hides these rows from every other read in the product; and the flag is
    /// then demanded in the open, because without it lifting the filter would
    /// serve the live half of the table to a screen that exists for the other half.
    ///
    /// The search matches a name or an address, and goes through the shared
    /// pattern builder: a support letter quoting an address with an underscore in
    /// it would otherwise match every neighbouring name.
    /// </remarks>
    private IQueryable<DbUser> Removed(string? search)
    {
        var removed = _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.IsRemoved);

        if (string.IsNullOrWhiteSpace(search))
        {
            return removed;
        }

        var pattern = LikePatterns.Contains(search.Trim());
        return removed.Where(user =>
            EF.Functions.ILike(user.Username, pattern) ||
            EF.Functions.ILike(user.Email, pattern));
    }
}
