using System;
using System.Diagnostics;
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
/// Opens the DB connections and compiles the hottest EF Core queries on startup.
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
        // Not awaited and never fatal: a cold query is slower, not wrong.
        _ = Task.Run(async () =>
        {
            _logger.LogInformation("[Warmup] Starting application warmup...");
            var started = Stopwatch.GetTimestamp();

            try
            {
                await WarmupDb(cancellationToken);

                _logger.LogInformation("[Warmup] Completed in {Duration}ms",
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Warmup] Failed (non-critical)");
            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    private async Task WarmupDb(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await db.Games.Where(g => !g.IsRemoved && g.Status == ModuleStatus.Active)
            .Take(10).Select(g => g.GameId).ToListAsync(ct);

        await db.Tags.CountAsync(ct);

        // The hottest query on the site: every authenticated request starts with it.
        await db.UserSessions
            .Where(s => s.SessionId == Guid.Empty && s.UserId == Guid.Empty)
            .FirstOrDefaultAsync(ct);

        // The storage filters the repository applies, so the warmed query is the one served.
        var dummyUserId = Guid.Empty;
        await db.Games
            .Where(GameAccessibilityFilters.GameAvailable(dummyUserId))
            .Where(GameParticipationFilters.Participating(db, dummyUserId))
            .ProjectToGame()
            .Take(1)
            .ToListAsync(ct);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
