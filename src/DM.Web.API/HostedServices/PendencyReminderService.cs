using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.PostPendencies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs post pendency reminders on a schedule.
/// </summary>
/// <remarks>
/// When a player is prodded and how often belongs to
/// <see cref="IPendencyReminderProcessor" />; this only decides how often to ask.
/// </remarks>
internal class PendencyReminderService : PeriodicHostedService
{
    private readonly ILogger<PendencyReminderService> _logger;

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

        var sent = await scope
            .GetRequiredService<IPendencyReminderProcessor>()
            .SendDueRemindersAsync(cancellationToken);

        if (sent == 0)
        {
            _logger.LogDebug("[Pendency Reminder] No stale pendencies found");
            return;
        }

        _logger.LogInformation("[Pendency Reminder] Sent {Count} reminders", sent);
    }
}
