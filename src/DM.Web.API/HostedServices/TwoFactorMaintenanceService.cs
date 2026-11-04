using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.TwoFactor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the two passes the second factor owes to the clock: abandoned setups
/// disappear, and removals whose waiting period has run out happen.
/// </summary>
/// <remarks>
/// One service, because both run on the same schedule over the same table. How
/// long each window is belongs to the domain; this only decides how often to ask.
/// </remarks>
internal class TwoFactorMaintenanceService(
    IServiceProvider serviceProvider,
    ILogger<TwoFactorMaintenanceService> logger)
    : PeriodicHostedService(serviceProvider, logger)
{
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
            Logger.LogInformation("{Tag} Deleted {Count} abandoned second-factor setups", Tag, abandoned);
        }

        var removed = await scope
            .GetRequiredService<ITwoFactorRemovalService>()
            .RunDue(cancellationToken);
        if (removed > 0)
        {
            Logger.LogWarning(
                "{Tag} Removed the second factor of {Count} accounts after the waiting period", Tag, removed);
        }
    }
}
