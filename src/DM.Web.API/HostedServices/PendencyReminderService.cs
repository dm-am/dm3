using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Messaging.GeneralBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that periodically sends reminders for unfulfilled post pendencies
/// </summary>
internal class PendencyReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendencyReminderService> _logger;

    /// <summary>
    /// How often to check for stale pendencies (every 12 hours)
    /// </summary>
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(12);

    /// <summary>
    /// Minimum age of pendency before sending first reminder (3 days)
    /// </summary>
    private readonly TimeSpan _firstReminderAfter = TimeSpan.FromDays(3);

    /// <summary>
    /// Minimum interval between reminders for the same pendency (3 days)
    /// </summary>
    private readonly TimeSpan _reminderInterval = TimeSpan.FromDays(3);

    public PendencyReminderService(
        IServiceProvider serviceProvider,
        ILogger<PendencyReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Pendency Reminder] Service started. Will check every {Interval} hours", _checkInterval.TotalHours);

        using var timer = new PeriodicTimer(_checkInterval);

        // Wait a bit on startup to let other services initialize
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        // Run initial check
        await SendReminders(stoppingToken);

        // Then run periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await SendReminders(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Pendency Reminder] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Pendency Reminder] Unexpected error in reminder loop");
                // Continue running despite errors
            }
        }
    }

    private async Task SendReminders(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[Pendency Reminder] Checking for stale pendencies");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var producer = scope.ServiceProvider.GetRequiredService<IInvokedEventProducer>();

            var now = DateTimeOffset.UtcNow;
            var oldestAllowed = now - _firstReminderAfter;
            var lastReminderCutoff = now - _reminderInterval;

            // Find unfulfilled pendencies that are old enough and haven't been reminded recently
            var stalePendencies = await dbContext.PostPendencies
                .Include(p => p.Room)
                .ThenInclude(r => r.Game)
                .Where(p => !p.IsRemoved &&
                            p.FulfilledUtc == null &&
                            p.CreatedUtc < oldestAllowed &&
                            (p.LastReminderUtc == null || p.LastReminderUtc < lastReminderCutoff) &&
                            p.Room.Game!.Status == ModuleStatus.Active)
                .ToListAsync(cancellationToken);

            if (stalePendencies.Count == 0)
            {
                _logger.LogDebug("[Pendency Reminder] No stale pendencies found");
                return;
            }

            _logger.LogInformation("[Pendency Reminder] Found {Count} stale pendencies, sending reminders", stalePendencies.Count);

            foreach (var pendency in stalePendencies)
            {
                pendency.LastReminderUtc = now;
                await producer.Send(EventType.RoomPendencyReminder, pendency.PendencyId);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("[Pendency Reminder] Sent {Count} reminders", stalePendencies.Count);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw to be caught by outer handler
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pendency Reminder] Error during reminder check");
            // Don't throw - we want the service to continue running
        }
    }
}
