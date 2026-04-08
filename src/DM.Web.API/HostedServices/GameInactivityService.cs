using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Inactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that processes inactive games:
/// - Warns active games with no posts for 1 month
/// - Freezes warned games after 1 week
/// - Warns frozen games after 3 months
/// - Closes frozen games 1 week after warning
/// </summary>
internal class GameInactivityService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameInactivityService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public GameInactivityService(
        IServiceProvider serviceProvider,
        ILogger<GameInactivityService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Game Inactivity] Service started. Will run every {Interval} hour(s)", _checkInterval.TotalHours);

        using var timer = new PeriodicTimer(_checkInterval);

        // Run initial processing on startup (after a short delay to let other services start)
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        await ProcessInactiveGames(stoppingToken);

        // Then run periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await ProcessInactiveGames(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Game Inactivity] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Game Inactivity] Unexpected error in processing loop");
                // Continue running despite errors
            }
        }
    }

    private async Task ProcessInactiveGames(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("[Game Inactivity] Starting inactivity processing");

            using var scope = _serviceProvider.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IGameInactivityProcessor>();

            // Process in order: warnings first, then actions
            await processor.WarnInactiveGamesAsync(ct);
            await processor.FreezeWarnedGamesAsync(ct);
            await processor.WarnFrozenGamesAsync(ct);
            await processor.CloseFrozenGamesAsync(ct);

            _logger.LogDebug("[Game Inactivity] Completed inactivity processing");
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw to be caught by outer handler
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Game Inactivity] Error during inactivity processing");
            // Don't throw - we want the service to continue running
        }
    }
}
