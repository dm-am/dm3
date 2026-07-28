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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that periodically updates popularity scores for games and blogs.
/// Game popularity = sum of active users among players and readers
///   - Active players: unique authors of active characters who were active on site within 30 days
///   - Active readers: subscribers who were active on site within 30 days
/// Blog popularity = active readers (subscribers active within 30 days)
/// </summary>
internal class PopularityScoreService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PopularityScoreService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan ActivePeriod = TimeSpan.FromDays(30);

    public PopularityScoreService(
        IServiceProvider serviceProvider,
        ILogger<PopularityScoreService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Popularity Score] Service started. Will run every {Interval} hour(s). Initial calculation done by WarmupService.", _updateInterval.TotalHours);

        using var timer = new PeriodicTimer(_updateInterval);

        // Only periodic updates - initial calculation is done by WarmupService
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await UpdatePopularityScores(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Popularity Score] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Popularity Score] Unexpected error in update loop");
            }
        }
    }

    private async Task UpdatePopularityScores(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var now = dateTimeProvider.Now;
        var activeThreshold = now - ActivePeriod;

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
