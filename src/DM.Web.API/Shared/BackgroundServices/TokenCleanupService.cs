using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Shared.BackgroundServices;

/// <summary>
/// Background service that periodically cleans up expired and removed tokens from the database
/// </summary>
internal class TokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TokenCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);

    public TokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<TokenCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Token Cleanup] Service started. Will run every {Interval} hours", _cleanupInterval.TotalHours);

        using var timer = new PeriodicTimer(_cleanupInterval);

        // Run initial cleanup on startup
        await CleanupTokens(stoppingToken);

        // Then run periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await CleanupTokens(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping, this is expected
                _logger.LogInformation("[Token Cleanup] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Token Cleanup] Unexpected error in cleanup loop");
                // Continue running despite errors
            }
        }
    }

    private async Task CleanupTokens(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[Token Cleanup] Starting token cleanup");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7);

            var deletedCount = await dbContext.Tokens
                .Where(t => t.IsRemoved || t.CreatedUtc < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation("[Token Cleanup] Deleted {Count} expired or removed tokens (older than {CutoffDate})",
                    deletedCount, cutoffDate);
            }
            else
            {
                _logger.LogDebug("[Token Cleanup] No tokens to clean up");
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw to be caught by outer handler
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Token Cleanup] Error during token cleanup");
            // Don't throw - we want the service to continue running
        }
    }
}
