using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Digests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the period digest generation on a schedule.
/// </summary>
/// <remarks>
/// Which periods get a digest, what it is called, where it is posted and how it
/// stays idempotent belong to <see cref="IPeriodDigestProcessor" />; this only
/// decides how often to ask.
/// </remarks>
internal class PeriodDigestService : PeriodicHostedService
{
    private readonly ILogger<PeriodDigestService> _logger;

    public PeriodDigestService(
        IServiceProvider serviceProvider,
        ILogger<PeriodDigestService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Period Digest]";

    /// <inheritdoc />
    /// <remarks>
    /// Month-boundary polling: the digest must appear shortly after midnight on
    /// the 1st; an hourly check is cheap (one indexed marker query).
    /// </remarks>
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var created = await scope
            .GetRequiredService<IPeriodDigestProcessor>()
            .EnsureClosedPeriodsAsync(cancellationToken);

        foreach (var title in created)
        {
            _logger.LogInformation("[Period Digest] Created digest topic \"{Title}\"", title);
        }
    }
}
