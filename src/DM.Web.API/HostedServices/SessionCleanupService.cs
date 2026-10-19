using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the expired-session purge on a schedule.
/// </summary>
/// <remarks>
/// What counts as expired and what the purge touches belongs to
/// <see cref="ISessionCleanupProcessor" />; this only decides how often to ask.
/// </remarks>
internal class SessionCleanupService(IServiceProvider serviceProvider, ILogger<SessionCleanupService> logger)
    : PeriodicHostedService(serviceProvider, logger)
{
    /// <inheritdoc />
    protected override string Tag => "[Session Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var purged = await scope
            .GetRequiredService<ISessionCleanupProcessor>()
            .PurgeExpiredAsync(cancellationToken);

        if (purged.SessionsRemoved > 0)
        {
            Logger.LogInformation("{Tag} Removed {Count} expired session(s)", Tag, purged.SessionsRemoved);
        }
    }
}
