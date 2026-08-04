using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.RelationalStorage;
using DomainGame = DM.Domain.Game.Features.Games.Game;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace DM.Web.API.HostedServices;

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
                // Phase 1: DB, MongoDB and AutoMapper warmup (all in parallel)
                var tasks = new[]
                {
                    WarmupDb(cancellationToken),
                    WarmupMongo(cancellationToken),
                    Task.Run(() => WarmupAutoMapper(), cancellationToken)
                };

                await Task.WhenAll(tasks);

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
