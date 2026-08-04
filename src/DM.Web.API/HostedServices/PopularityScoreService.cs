using DM.Domain.Core.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that periodically updates popularity scores for games and blogs.
/// Game popularity = sum of active users among players and readers
///   - Active players: unique authors of active characters who were active on site within 30 days
///   - Active readers: subscribers who were active on site within 30 days
/// Blog popularity = active readers (subscribers active within 30 days)
/// </summary>
internal class PopularityScoreService : PeriodicHostedService
{
    private readonly ILogger<PopularityScoreService> _logger;

    public PopularityScoreService(
        IServiceProvider serviceProvider,
        ILogger<PopularityScoreService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Popularity Score]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    /// <remarks>
    /// The first calculation happens on the first pass rather than being deferred
    /// to the first tick. It used to be deferred on the strength of a comment
    /// saying WarmupService did it at startup — that phase was deleted with the
    /// duplicated copy of WarmupService it lived in, and nothing noticed, because
    /// a stale score looks exactly like a correct one. The result was that
    /// "популярные игры" and "популярные блоги" served whatever the previous run
    /// had persisted for a full hour after every cold start, and zeroes for that
    /// hour on a freshly reset database.
    ///
    /// The service that owns the calculation owns its first run: the coupling to
    /// a warmup phase in another service is what allowed the gap to open.
    /// </remarks>
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var dbContext = scope.GetRequiredService<DmDbContext>();
        var dateTimeProvider = scope.GetRequiredService<IDateTimeProvider>();

        var now = dateTimeProvider.Now;
        var activeThreshold = now - ActivityPolicy.ActivePeriod;

        await UpdateGameScores(dbContext, now, activeThreshold, cancellationToken);
        await UpdateBlogScores(dbContext, now, activeThreshold, cancellationToken);
    }

    private async Task UpdateGameScores(
        DmDbContext dbContext,
        DateTimeOffset now,
        DateTimeOffset activeThreshold,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all active (non-removed, non-draft) games
            var gameIds = await dbContext.Games
                .Where(g => !g.IsRemoved && g.Status != ModuleStatus.Draft)
                .Select(g => g.GameId)
                .ToListAsync(cancellationToken);

            if (gameIds.Count == 0)
            {
                _logger.LogDebug("[Popularity Score] No games to update");
                return;
            }

            // Calculate active players per game (unique authors of active non-NPC characters who were active on site)
            var playerCounts = await dbContext.Characters
                .Where(c => gameIds.Contains(c.GameId) &&
                           c.Status == CharacterStatus.Active &&
                           !c.IsNpc &&
                           c.AuthorId.HasValue &&
                           c.Author != null &&
                           c.Author.LastActivityUtc.HasValue &&
                           c.Author.LastActivityUtc.Value > activeThreshold)
                .GroupBy(c => c.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    PlayerCount = g.Select(c => c.AuthorId!.Value).Distinct().Count()
                })
                .ToDictionaryAsync(x => x.GameId, x => x.PlayerCount, cancellationToken);

            // Calculate active readers per game (subscribers who were active within 30 days)
            var readerCounts = await dbContext.Subscriptions
                .Where(s => s.TargetType == SubscriptionTargetType.Game &&
                           gameIds.Contains(s.TargetId) &&
                           s.Subscriber.LastActivityUtc.HasValue &&
                           s.Subscriber.LastActivityUtc.Value > activeThreshold)
                .GroupBy(s => s.TargetId)
                .Select(g => new
                {
                    GameId = g.Key,
                    ReaderCount = g.Count()
                })
                .ToDictionaryAsync(x => x.GameId, x => x.ReaderCount, cancellationToken);

            // Update games in batches
            var updatedCount = 0;
            const int batchSize = 100;

            foreach (var batch in gameIds.Chunk(batchSize))
            {
                var games = await dbContext.Games
                    .Where(g => batch.Contains(g.GameId))
                    .ToListAsync(cancellationToken);

                foreach (var game in games)
                {
                    playerCounts.TryGetValue(game.GameId, out var playerCount);
                    readerCounts.TryGetValue(game.GameId, out var readerCount);
                    var newScore = playerCount + readerCount;

                    if (game.PopularityScore != newScore)
                    {
                        game.PopularityScore = newScore;
                        game.PopularityScoreUpdatedUtc = now;
                        updatedCount++;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("[Popularity Score] Updated {UpdatedCount} of {TotalCount} games",
                updatedCount, gameIds.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Popularity Score] Error updating game scores");
        }
    }

    private async Task UpdateBlogScores(
        DmDbContext dbContext,
        DateTimeOffset now,
        DateTimeOffset activeThreshold,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all non-removed blogs (include Active and Closed - finished blogs may have historical readers)
            var blogIds = await dbContext.Blogs
                .Where(b => !b.IsRemoved)
                .Select(b => b.BlogId)
                .ToListAsync(cancellationToken);

            if (blogIds.Count == 0)
            {
                _logger.LogDebug("[Popularity Score] No blogs to update");
                return;
            }

            // Calculate active readers per blog (subscribers who were active within 30 days)
            var readerCounts = await dbContext.Subscriptions
                .Where(s => s.TargetType == SubscriptionTargetType.Blog &&
                           blogIds.Contains(s.TargetId) &&
                           s.Subscriber.LastActivityUtc.HasValue &&
                           s.Subscriber.LastActivityUtc.Value > activeThreshold)
                .GroupBy(s => s.TargetId)
                .Select(g => new
                {
                    BlogId = g.Key,
                    ReaderCount = g.Count()
                })
                .ToDictionaryAsync(x => x.BlogId, x => x.ReaderCount, cancellationToken);

            // Update blogs in batches
            var updatedCount = 0;
            const int batchSize = 100;

            foreach (var batch in blogIds.Chunk(batchSize))
            {
                var blogs = await dbContext.Blogs
                    .Where(b => batch.Contains(b.BlogId))
                    .ToListAsync(cancellationToken);

                foreach (var blog in blogs)
                {
                    readerCounts.TryGetValue(blog.BlogId, out var readerCount);
                    var newScore = readerCount;

                    if (blog.PopularityScore != newScore)
                    {
                        blog.PopularityScore = newScore;
                        blog.PopularityScoreUpdatedUtc = now;
                        updatedCount++;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("[Popularity Score] Updated {UpdatedCount} of {TotalCount} blogs",
                updatedCount, blogIds.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Popularity Score] Error updating blog scores");
        }
    }
}
