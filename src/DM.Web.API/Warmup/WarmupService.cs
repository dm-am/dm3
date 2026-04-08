using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Moderation.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.RelationalStorage;
using DomainGame = DM.Domain.Game.Features.Games.Game;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DM.Web.API.Warmup;

/// <summary>
/// Warmup service that preloads DB connections and JIT compiles critical paths on startup
/// </summary>
internal class WarmupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WarmupService> _logger;

    public WarmupService(
        IServiceProvider serviceProvider,
        ILogger<WarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            _logger.LogInformation("[Warmup] Starting application warmup...");
            var startTime = DateTime.UtcNow;

            try
            {
                // Validate configuration
                ValidateConfiguration();

                // Phase 1: DB, MongoDB and AutoMapper warmup (all in parallel)
                var tasks = new[]
                {
                    WarmupDb(cancellationToken),
                    WarmupMongo(cancellationToken),
                    Task.Run(() => WarmupAutoMapper(), cancellationToken)
                };

                await Task.WhenAll(tasks);

                // Phase 2: Calculate popularity scores (depends on DB being ready)
                await WarmupPopularityScores(cancellationToken);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("[Warmup] Completed in {Duration}ms", duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Warmup] Failed (non-critical): {Message}", ex.Message);
            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    private void ValidateConfiguration()
    {
        using var scope = _serviceProvider.CreateScope();
        var probationConfig = scope.ServiceProvider.GetRequiredService<IOptions<ProbationConfiguration>>().Value;

        if (probationConfig.NewbiePostThreshold != 100)
        {
            _logger.LogWarning(
                "[Warmup] ProbationConfiguration.NewbiePostThreshold is {Threshold} but DB computed column uses hardcoded 100. Run migration to sync.",
                probationConfig.NewbiePostThreshold);
        }
    }

    private void WarmupAutoMapper()
    {
        using var scope = _serviceProvider.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        mapper.ConfigurationProvider.CompileMappings();
    }

    private async Task WarmupDb(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

        // Warmup simple games query
        await db.Games.Where(g => !g.IsRemoved && g.Status == ModuleStatus.Active)
            .Take(10).Select(g => g.GameId).ToListAsync(ct);

        // Warmup tags
        await db.Tags.CountAsync(ct);

        // Warmup the complex GetOwn query with ProjectTo<Game>
        // This pre-compiles the EF Core query and AutoMapper projection
        var dummyUserId = Guid.Empty;
        await db.Games
            .Where(GameAccessibilityFilters.GameAvailable(dummyUserId))
            .Where(g => g.Characters.Any(c =>
                            !c.IsRemoved && c.Status == CharacterStatus.Active && c.AuthorId == dummyUserId) ||
                        db.Subscriptions.Any(s =>
                            s.TargetType == SubscriptionTargetType.Game &&
                            s.TargetId == g.GameId &&
                            s.SubscriberId == dummyUserId) ||
                        g.MasterId == dummyUserId || g.Assistants.Any(a => a.UserId == dummyUserId) || g.MentorId == dummyUserId)
            .ProjectTo<DomainGame>(mapper.ConfigurationProvider)
            .Take(1)
            .ToListAsync(ct);

        _logger.LogDebug("[Warmup] EF Core queries compiled");
    }

    private async Task WarmupMongo(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var mongoClient = scope.ServiceProvider.GetRequiredService<DmMongoClient>();

        // Warmup MongoDB connection and UnreadCounters collection
        var collection = mongoClient.GetCollection<UnreadCounter>();
        await collection.Find(c => c.UserId == Guid.Empty)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        _logger.LogDebug("[Warmup] MongoDB connection established");
    }

    /// <summary>
    /// Calculate popularity scores for games and blogs on startup.
    /// This ensures sidebar data is ready before first request.
    /// </summary>
    private async Task WarmupPopularityScores(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var now = dateTimeProvider.Now;
        var activeThreshold = now - TimeSpan.FromDays(30);

        // Update game popularity scores
        var gameIds = await db.Games
            .Where(g => !g.IsRemoved && g.Status != ModuleStatus.Draft)
            .Select(g => g.GameId)
            .ToListAsync(ct);

        if (gameIds.Count > 0)
        {
            var playerCounts = await db.Characters
                .Include(c => c.Author)
                .Where(c => gameIds.Contains(c.GameId) &&
                           c.Status == CharacterStatus.Active &&
                           !c.IsNpc &&
                           c.AuthorId.HasValue &&
                           c.Author != null &&
                           c.Author.LastActivityUtc.HasValue &&
                           c.Author.LastActivityUtc.Value > activeThreshold)
                .GroupBy(c => c.GameId)
                .Select(g => new { GameId = g.Key, Count = g.Select(c => c.AuthorId!.Value).Distinct().Count() })
                .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

            var gameReaderCounts = await db.Subscriptions
                .Where(s => s.TargetType == SubscriptionTargetType.Game &&
                           gameIds.Contains(s.TargetId) &&
                           s.Subscriber.LastActivityUtc.HasValue &&
                           s.Subscriber.LastActivityUtc.Value > activeThreshold)
                .GroupBy(s => s.TargetId)
                .Select(g => new { GameId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

            var games = await db.Games.Where(g => gameIds.Contains(g.GameId)).ToListAsync(ct);
            foreach (var game in games)
            {
                playerCounts.TryGetValue(game.GameId, out var playerCount);
                gameReaderCounts.TryGetValue(game.GameId, out var readerCount);
                game.PopularityScore = playerCount + readerCount;
                game.PopularityScoreUpdatedUtc = now;
            }
            await db.SaveChangesAsync(ct);
        }

        // Update blog popularity scores
        var blogIds = await db.Blogs
            .Where(b => !b.IsRemoved)
            .Select(b => b.BlogId)
            .ToListAsync(ct);

        if (blogIds.Count > 0)
        {
            var blogReaderCounts = await db.Subscriptions
                .Where(s => s.TargetType == SubscriptionTargetType.Blog &&
                           blogIds.Contains(s.TargetId) &&
                           s.Subscriber.LastActivityUtc.HasValue &&
                           s.Subscriber.LastActivityUtc.Value > activeThreshold)
                .GroupBy(s => s.TargetId)
                .Select(g => new { BlogId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BlogId, x => x.Count, ct);

            var blogs = await db.Blogs.Where(b => blogIds.Contains(b.BlogId)).ToListAsync(ct);
            foreach (var blog in blogs)
            {
                blogReaderCounts.TryGetValue(blog.BlogId, out var readerCount);
                blog.PopularityScore = readerCount;
                blog.PopularityScoreUpdatedUtc = now;
            }
            await db.SaveChangesAsync(ct);
        }

        _logger.LogDebug("[Warmup] Popularity scores calculated for {GameCount} games and {BlogCount} blogs",
            gameIds.Count, blogIds.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
