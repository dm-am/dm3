using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Uploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the orphaned-upload sweep on a schedule.
/// </summary>
/// <remarks>
/// The grace period, the batch and the "keep the row if the store refused" rule
/// belong to <see cref="IUploadOrphanCollector" />; this only decides how often
/// to ask.
/// </remarks>
internal class UploadOrphanCleanupService : PeriodicHostedService
{
    private readonly ILogger<UploadOrphanCleanupService> _logger;

    public UploadOrphanCleanupService(
        IServiceProvider serviceProvider,
        ILogger<UploadOrphanCleanupService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Upload Orphan Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(6);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var swept = await scope
            .GetRequiredService<IUploadOrphanCollector>()
            .SweepAsync(cancellationToken);

        if (swept.Candidates == 0)
        {
            _logger.LogDebug("[Upload Orphan Cleanup] No orphans to remove");
            return;
        }

        _logger.LogInformation(
            "[Upload Orphan Cleanup] Swept {Candidates} candidate(s): removed {ObjectCount} objects + {DbCount} DB rows",
            swept.Candidates, swept.ObjectsRemoved, swept.RecordsDeleted);
    }
}
