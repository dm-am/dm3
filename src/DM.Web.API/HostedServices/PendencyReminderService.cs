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
internal class PendencyReminderService(IServiceProvider serviceProvider, ILogger<PendencyReminderService> logger)
    : PeriodicHostedService(serviceProvider, logger)
{
    /// <inheritdoc />
    protected override string Tag => "[Pendency Reminder]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(12);

    /// <inheritdoc />
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var sent = await scope
            .GetRequiredService<IPendencyReminderProcessor>()
            .SendDueRemindersAsync(cancellationToken);

        if (sent > 0)
        {
            Logger.LogInformation("{Tag} Sent {Count} reminders", Tag, sent);
        }
    }
}
