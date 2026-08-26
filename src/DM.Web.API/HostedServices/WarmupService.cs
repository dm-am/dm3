using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Repositories.Game;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                // Mapping is compile-time (Mapperly) since the AutoMapper
                // retirement, so the DB is the one thing left to warm.
                await WarmupDb(cancellationToken);

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

    private async Task WarmupDb(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        // Warmup simple games query
        await db.Games.Where(g => !g.IsRemoved && g.Status == ModuleStatus.Active)
            .Take(10).Select(g => g.GameId).ToListAsync(ct);

        // Warmup tags
        await db.Tags.CountAsync(ct);

        // Warmup the session lookup - the hottest query on the site: every
        // authenticated request starts with this primary-key read.
        await db.UserSessions
            .Where(s => s.SessionId == Guid.Empty && s.UserId == Guid.Empty)
            .FirstOrDefaultAsync(ct);

        // Warmup the participating games list through the game projection.
        // This pre-compiles the EF Core query. Both predicates are the storage
        // filters the repository itself applies, so the product rules in this
        // query cannot drift from the rules the list is served with.
        var dummyUserId = Guid.Empty;
        await db.Games
            .Where(GameAccessibilityFilters.GameAvailable(dummyUserId))
            .Where(GameParticipationFilters.Participating(db, dummyUserId))
            .ProjectToGame()
            .Take(1)
            .ToListAsync(ct);

        _logger.LogDebug("[Warmup] EF Core queries compiled");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
