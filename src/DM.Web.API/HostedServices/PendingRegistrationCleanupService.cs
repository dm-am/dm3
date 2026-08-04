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
internal class PendingRegistrationCleanupService : PeriodicHostedService
{
    private readonly ILogger<PendingRegistrationCleanupService> _logger;

    /// <summary>
    /// Creates a new instance of the cleanup service
    /// </summary>
    public PendingRegistrationCleanupService(
        IServiceProvider serviceProvider,
        ILogger<PendingRegistrationCleanupService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Pending Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Pending Cleanup] Starting cleanup of expired pending registrations");

        var deletedCount = await scope
            .GetRequiredService<IPendingRegistrationCleanupProcessor>()
            .DeleteExpiredAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "[Pending Cleanup] Deleted {Count} expired pending registrations", deletedCount);
        }
        else
        {
            _logger.LogDebug("[Pending Cleanup] No expired pending registrations to clean up");
        }
    }
}
