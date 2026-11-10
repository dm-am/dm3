using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

internal partial class UserRepository
{
    /// <inheritdoc />
    public Task<IReadOnlyCollection<Guid>> GetRemovedAccountIdsAsync() =>
        RemovedAccountScope.Ids(_dbContext.Users);

    /// <inheritdoc />
    /// <remarks>
    /// The games and blogs are counted here rather than added up from
    /// <see cref="PopulateListCounts" /> per account: a game with a removed master and
    /// a removed assistant, or with two removed players, would count twice, and the
    /// number on the tab would stop being the length of the list behind it. Each game
    /// and blog counts once, by the same predicates the per-account counters use.
    /// </remarks>
    public async Task<GeneralUser> GetRemovedAccountSummaryAsync()
    {
        var removed = await RemovedAccountScope.Ids(_dbContext.Users);
        var (quality, quantity) = await RemovedAccountScope.RatingTotals(_dbContext.Users);

        var reviewsGiven = await _dbContext.PostReviews
            .TagWith("DM.User.RemovedAccountSummary")
            .CountAsync(r => removed.Contains(r.AuthorId));
        var reviewsReceived = await _dbContext.PostReviews
            .TagWith("DM.User.RemovedAccountSummary")
            .CountAsync(r => removed.Contains(r.PostAuthorId));

        var gamesHosting = await _dbContext.Games
            .TagWith("DM.User.RemovedAccountSummary")
            .Where(g => removed.Contains(g.MasterId) || g.Assistants.Any(a => removed.Contains(a.UserId)))
            .GroupBy(g => g.Status)
            .Select(g => new StatusCountItem(Guid.Empty, g.Key, g.Count()))
            .ToListAsync();
        var gamesPlaying = await _dbContext.Games
            .TagWith("DM.User.RemovedAccountSummary")
            .Where(g => g.Characters.Any(c =>
                !c.IsNpc && c.AuthorId.HasValue && removed.Contains(c.AuthorId.Value)))
            .GroupBy(g => g.Status)
            .Select(g => new StatusCountItem(Guid.Empty, g.Key, g.Count()))
            .ToListAsync();
        var blogsHosting = await _dbContext.Blogs
            .TagWith("DM.User.RemovedAccountSummary")
            .Where(b => removed.Contains(b.AuthorId) || b.Assistants.Any(a => removed.Contains(a.UserId)))
            .GroupBy(b => b.Status)
            .Select(g => new StatusCountItem(Guid.Empty, g.Key, g.Count()))
            .ToListAsync();

        var gamesHostingByStatus = BuildStatusBreakdownDict(gamesHosting, null).GetValueOrDefault(Guid.Empty);
        var gamesPlayingByStatus = BuildStatusBreakdownDict(gamesPlaying, null).GetValueOrDefault(Guid.Empty);
        var blogsHostingByStatus = BuildStatusBreakdownDict(blogsHosting, null).GetValueOrDefault(Guid.Empty);

        return new GeneralUser
        {
            UserId = Guid.Empty,
            Username = RemovedAccount.Name,
            Role = UserRole.RegularUser,
            QualityRating = quality,
            QuantityRating = quantity,
            PostReviewsGivenCount = reviewsGiven,
            PostReviewsReceivedCount = reviewsReceived,
            GamesHostingByStatus = gamesHostingByStatus,
            GamesHosting = gamesHostingByStatus?.Total ?? 0,
            GamesPlayingByStatus = gamesPlayingByStatus,
            GamesPlaying = gamesPlayingByStatus?.Total ?? 0,
            BlogsHostingByStatus = blogsHostingByStatus,
            BlogsHosting = blogsHostingByStatus?.Total ?? 0,
        };
    }
}
