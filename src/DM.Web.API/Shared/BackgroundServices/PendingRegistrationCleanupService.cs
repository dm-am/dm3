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
/// Background service that periodically cleans up pending registrations older than 7 days.
/// This frees up email addresses for re-registration after the registration window expires.
/// </summary>
/// <remarks>
/// With the email-first registration flow, users are not created in the Users table
/// until they complete activation by choosing a login. PendingRegistration entries
/// store the email and password hash until activation.
/// </remarks>
internal class PendingRegistrationCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingRegistrationCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly int _registrationExpirationDays = 7;

    /// <summary>
    /// Creates a new instance of the cleanup service
    /// </summary>
    public PendingRegistrationCleanupService(
        IServiceProvider serviceProvider,
        ILogger<PendingRegistrationCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[Pending Cleanup] Service started. Will run every {Interval} hour(s), removing registrations older than {Days} days",
            _cleanupInterval.TotalHours,
            _registrationExpirationDays);

        using var timer = new PeriodicTimer(_cleanupInterval);

        // Run initial cleanup on startup
        await CleanupPendingRegistrations(stoppingToken);

        // Then run periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await CleanupPendingRegistrations(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping, this is expected
                _logger.LogInformation("[Pending Cleanup] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Pending Cleanup] Unexpected error in cleanup loop");
                // Continue running despite errors
            }
        }
    }

    private async Task CleanupPendingRegistrations(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[Pending Cleanup] Starting cleanup of expired pending registrations");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-_registrationExpirationDays);

            // Delete pending registrations older than the cutoff date
            // Note: No FK constraints to worry about - PendingRegistration is standalone
            var deletedCount = await dbContext.PendingRegistrations
                .Where(p => p.CreatedUtc < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "[Pending Cleanup] Deleted {Count} expired pending registrations older than {CutoffDate}",
                    deletedCount,
                    cutoffDate);
            }
            else
            {
                _logger.LogDebug("[Pending Cleanup] No expired pending registrations to clean up");
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw to be caught by outer handler
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pending Cleanup] Error during pending registration cleanup");
            // Don't throw - we want the service to continue running
        }
    }
}
