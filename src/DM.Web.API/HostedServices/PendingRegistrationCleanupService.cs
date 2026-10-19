using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the pending-registration window on a schedule.
/// </summary>
/// <remarks>
/// How long an unfinished registration holds its email address belongs to
/// <see cref="IPendingRegistrationCleanupProcessor" />; this only decides how
/// often to ask.
/// </remarks>
internal class PendingRegistrationCleanupService(
    IServiceProvider serviceProvider,
    ILogger<PendingRegistrationCleanupService> logger)
    : PeriodicHostedService(serviceProvider, logger)
{
    /// <inheritdoc />
    protected override string Tag => "[Pending Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var deletedCount = await scope
            .GetRequiredService<IPendingRegistrationCleanupProcessor>()
            .DeleteExpiredAsync(cancellationToken);

        if (deletedCount > 0)
        {
            Logger.LogInformation("{Tag} Deleted {Count} expired pending registrations", Tag, deletedCount);
        }
    }
}
