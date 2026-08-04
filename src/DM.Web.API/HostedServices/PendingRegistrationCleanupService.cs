using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that periodically cleans up pending registrations older than 7 days.
/// This frees up email addresses for re-registration after the registration window expires.
/// </summary>
/// <remarks>
/// With the email-first registration flow, users are not created in the Users table
/// until they complete activation by choosing a username. PendingRegistration entries
/// store the email and password hash until activation.
/// </remarks>
internal class PendingRegistrationCleanupService : PeriodicHostedService
{
    private readonly ILogger<PendingRegistrationCleanupService> _logger;
    private readonly int _registrationExpirationDays = 7;

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

        var dbContext = scope.GetRequiredService<DmDbContext>();

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-_registrationExpirationDays);

        // Delete pending registrations older than the cutoff date
        // Note: No FK constraints to worry about - PendingRegistration is standalone
        var deletedCount = await dbContext.PendingRegistrations
            .Where(p => p.CreatedUtc < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "[Pending Cleanup] Deleted {Count} expired pending registrations older than {CutoffDate}",
                deletedCount,
                cutoffDate);
        }
        else
        {
            _logger.LogDebug("[Pending Cleanup] No expired pending registrations to clean up");
        }
    }
}
