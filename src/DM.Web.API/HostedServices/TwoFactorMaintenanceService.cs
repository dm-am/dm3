using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.TwoFactor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the two passes the second factor owes to the clock.
/// </summary>
/// <remarks>
/// One service and not two, because they run on the same schedule over the same
/// table and neither is worth a loop of its own: a setup nobody finished has to
/// disappear, and a removal whose waiting period has run out has to happen.
///
/// How long each of those windows is belongs to the domain; this only decides
/// how often to ask.
/// </remarks>
internal class TwoFactorMaintenanceService : PeriodicHostedService
{
    private readonly ILogger<TwoFactorMaintenanceService> _logger;

    /// <summary>
    /// Creates a new instance of the maintenance service
    /// </summary>
    public TwoFactorMaintenanceService(
        IServiceProvider serviceProvider,
        ILogger<TwoFactorMaintenanceService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[2FA Maintenance]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var abandoned = await scope
            .GetRequiredService<ITwoFactorCleanupProcessor>()
            .DeleteAbandonedAsync(cancellationToken);
        if (abandoned > 0)
        {
            _logger.LogInformation(
                "[2FA Maintenance] Deleted {Count} abandoned second-factor setups", abandoned);
        }

        var removed = await scope
            .GetRequiredService<ITwoFactorRemovalService>()
            .RunDue(cancellationToken);
        if (removed > 0)
        {
            _logger.LogWarning(
                "[2FA Maintenance] Removed the second factor of {Count} accounts after the " +
                "waiting period", removed);
        }
    }
}
