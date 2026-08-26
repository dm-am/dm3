using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the retention sweep on a schedule: the replacement for the TTL indexes
/// of the retired document store.
/// </summary>
/// <remarks>
/// Which tables carry a term and how long each is belongs to the persistence
/// layer's retention registry, behind <see cref="IRetentionSweepProcessor" />;
/// this only decides how often to ask. An hour is not worse than the TTL
/// monitor was: the reading side never depends on the deletion moment — the
/// lockout counts by LastAttemptUtc, not by the row being gone — and a failed
/// pass is retried by the next one.
/// </remarks>
internal class RetentionSweepService : PeriodicHostedService
{
    private readonly ILogger<RetentionSweepService> _logger;

    public RetentionSweepService(
        IServiceProvider serviceProvider,
        ILogger<RetentionSweepService> logger)
        : base(serviceProvider, logger) => _logger = logger;

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
                _logger.LogInformation("[Retention Sweep] Removed {Count} expired row(s) from {Table}",
                    result.RowsDeleted, result.Table);
            }
        }
    }
}
