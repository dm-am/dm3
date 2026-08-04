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
internal class UsernameChangeCleanupService : PeriodicHostedService
{
    private readonly ILogger<UsernameChangeCleanupService> _logger;

    public UsernameChangeCleanupService(
        IServiceProvider serviceProvider,
        ILogger<UsernameChangeCleanupService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Username Change Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    /// <remarks>
    /// The two steps are independent - one expires what moderators did not review,
    /// the other what users did not use - so each is guarded on its own. Letting
    /// the pass guard take both would make a failure in the first step skip the
    /// second for the whole interval.
    /// </remarks>
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var processor = scope.GetRequiredService<IUsernameChangeExpiryProcessor>();

        try
        {
            _logger.LogDebug("[Username Change Cleanup] Checking for expired pending requests");
            var expiredCount = await processor.ExpireUnreviewedAsync(cancellationToken);
            if (expiredCount > 0)
            {
                _logger.LogInformation(
                    "[Username Change Cleanup] Expired {Count} unreviewed requests", expiredCount);
            }
            else
            {
                _logger.LogDebug("[Username Change Cleanup] No expired pending requests");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Username Change Cleanup] Error expiring pending requests");
        }

        try
        {
            _logger.LogDebug("[Username Change Cleanup] Checking for expired approval tokens");
            var expiredCount = await processor.ExpireApprovalTokensAsync(cancellationToken);
            if (expiredCount > 0)
            {
                _logger.LogInformation(
                    "[Username Change Cleanup] Expired {Count} approval tokens", expiredCount);
            }
            else
            {
                _logger.LogDebug("[Username Change Cleanup] No expired approval tokens");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Username Change Cleanup] Error expiring approval tokens");
        }
    }
}
