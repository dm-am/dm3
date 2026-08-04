using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that periodically sends reminders for unfulfilled post pendencies
/// </summary>
internal class PendencyReminderService : PeriodicHostedService
{
    private readonly ILogger<PendencyReminderService> _logger;

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
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Pendency Reminder]";

    /// <inheritdoc />
    /// <remarks>How often to check for stale pendencies.</remarks>
    protected override TimeSpan Interval => TimeSpan.FromHours(12);

    /// <inheritdoc />
    /// <remarks>Lets the rest of the host settle before the first pass.</remarks>
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Pendency Reminder] Checking for stale pendencies");

        var dbContext = scope.GetRequiredService<DmDbContext>();
        var producer = scope.GetRequiredService<IEventProducer>();

        var now = DateTimeOffset.UtcNow;
        var oldestAllowed = now - _firstReminderAfter;
        var lastReminderCutoff = now - _reminderInterval;

        // Find unfulfilled pendencies that are old enough and haven't been reminded recently
        var stalePendencies = await dbContext.PostPendencies
            .Include(p => p.Room)
            .ThenInclude(r => r.Game)
            .Where(p =>
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
        }

        // Committed before anything is published. The marker is the only thing
        // that stops the same reminder going out on every pass, and until this
        // line it exists in the change tracker and nowhere else: a failure here
        // with the events already gone means letters and bot messages the
        // database holds no record of, sent again twelve hours later, and again
        // until one save finally lands.
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var pendency in stalePendencies)
        {
            await producer.SendAsync(EventType.RoomPendencyReminder, pendency.PendencyId);
        }

        _logger.LogInformation("[Pendency Reminder] Sent {Count} reminders", stalePendencies.Count);
    }
}
