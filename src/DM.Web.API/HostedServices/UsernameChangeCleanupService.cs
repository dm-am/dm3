using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.UsernameChange;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the two name change deadlines on a schedule.
/// </summary>
/// <remarks>
/// Both windows and both resolution comments belong to
/// <see cref="IUsernameChangeExpiryProcessor" />; this only decides how often to
/// ask, and keeps the two calls independent.
/// </remarks>
internal class UsernameChangeCleanupService(
    IServiceProvider serviceProvider,
    ILogger<UsernameChangeCleanupService> logger)
    : PeriodicHostedService(serviceProvider, logger)
{
    /// <inheritdoc />
    protected override string Tag => "[Username Change Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    /// <remarks>
    /// Each step is guarded on its own, so a failure in the first does not skip
    /// the second for the whole interval.
    /// </remarks>
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var processor = scope.GetRequiredService<IUsernameChangeExpiryProcessor>();

        await Guarded("expiring pending requests", async () =>
        {
            var expiredCount = await processor.ExpireUnreviewedAsync(cancellationToken);
            if (expiredCount > 0)
            {
                Logger.LogInformation("{Tag} Expired {Count} unreviewed requests", Tag, expiredCount);
            }
        });

        await Guarded("expiring approval tokens", async () =>
        {
            var expiredCount = await processor.ExpireApprovalTokensAsync(cancellationToken);
            if (expiredCount > 0)
            {
                Logger.LogInformation("{Tag} Expired {Count} approval tokens", Tag, expiredCount);
            }
        });
    }
}
