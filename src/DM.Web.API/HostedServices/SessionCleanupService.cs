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
internal class SessionCleanupService : PeriodicHostedService
{
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<SessionCleanupService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Session Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Session Cleanup] Starting session cleanup");

        var purged = await scope
            .GetRequiredService<ISessionCleanupProcessor>()
            .PurgeExpiredAsync(cancellationToken);

        if (purged.SessionsRemoved > 0)
        {
            _logger.LogInformation("[Session Cleanup] Removed {Count} expired session(s)",
                purged.SessionsRemoved);
        }
        else
        {
            _logger.LogDebug("[Session Cleanup] No sessions to clean up");
        }
    }
}
