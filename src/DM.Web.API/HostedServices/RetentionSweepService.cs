using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the retention sweep on a schedule.
/// </summary>
/// <remarks>
/// Which tables carry a term and how long each is belongs to the persistence
/// layer's retention registry, behind <see cref="IRetentionSweepProcessor" />;
/// this only decides how often to ask. An hour is enough: no reader depends on
/// the moment of deletion, and a failed pass is retried by the next one.
/// </remarks>
internal class RetentionSweepService(IServiceProvider serviceProvider, ILogger<RetentionSweepService> logger)
    : PeriodicHostedService(serviceProvider, logger)
{
    /// <inheritdoc />
    protected override string Tag => "[Retention Sweep]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var results = await scope
            .GetRequiredService<IRetentionSweepProcessor>()
            .SweepAsync(cancellationToken);

        foreach (var result in results)
        {
            if (result.RowsDeleted > 0)
            {
                Logger.LogInformation("{Tag} Removed {Count} expired row(s) from {Table}",
                    Tag, result.RowsDeleted, result.Table);
            }
        }
    }
}
