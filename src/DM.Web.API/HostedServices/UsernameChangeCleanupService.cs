using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that handles expiration of username change requests:
/// 1. Auto-rejects pending requests that moderators haven't reviewed within 7 days
/// 2. Expires approval tokens that users haven't used within 48 hours
/// </summary>
internal class UsernameChangeCleanupService : PeriodicHostedService
{
    private readonly ILogger<UsernameChangeCleanupService> _logger;
    private readonly int _pendingExpirationDays = 7;
    private readonly int _approvalTokenExpirationHours = 48;

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
        var dbContext = scope.GetRequiredService<DmDbContext>();

        try
        {
            await ExpirePendingRequests(dbContext, cancellationToken);
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
            await ExpireApprovalTokens(dbContext, cancellationToken);
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

    /// <summary>
    /// Auto-reject pending requests that moderators haven't reviewed within 7 days
    /// </summary>
    private async Task ExpirePendingRequests(DmDbContext dbContext, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Username Change Cleanup] Checking for expired pending requests");

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-_pendingExpirationDays);

        // Update pending requests older than 7 days to Expired status
        var expiredCount = await dbContext.UsernameChangeRequests
            .Where(r => r.Status == UsernameChangeRequestStatus.Pending &&
                       r.CreatedUtc < cutoffDate)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, UsernameChangeRequestStatus.Expired)
                      .SetProperty(r => r.ResolvedUtc, DateTimeOffset.UtcNow)
                      .SetProperty(r => r.ResolverComment, "Автоматически отклонено: истек срок ожидания модерации"),
                cancellationToken);

        if (expiredCount > 0)
        {
            _logger.LogInformation(
                "[Username Change Cleanup] Expired {Count} pending requests older than {Days} days",
                expiredCount,
                _pendingExpirationDays);
        }
        else
        {
            _logger.LogDebug("[Username Change Cleanup] No expired pending requests");
        }
    }

    /// <summary>
    /// Expire approval tokens that users haven't used within 48 hours
    /// </summary>
    private async Task ExpireApprovalTokens(DmDbContext dbContext, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "[Username Change Cleanup] Checking for approval tokens older than {Hours} hours",
            _approvalTokenExpirationHours);

        var now = DateTimeOffset.UtcNow;

        // Update approved requests with expired tokens to Expired status
        var expiredCount = await dbContext.UsernameChangeRequests
            .Where(r => r.Status == UsernameChangeRequestStatus.Approved &&
                       r.ApprovalTokenExpiresUtc.HasValue &&
                       r.ApprovalTokenExpiresUtc.Value < now)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, UsernameChangeRequestStatus.Expired)
                      .SetProperty(r => r.ApprovalToken, (Guid?)null)
                      .SetProperty(r => r.ResolverComment,
                          r => r.ResolverComment + " | Токен истек: пользователь не выбрал новое имя в отведенное время"),
                cancellationToken);

        if (expiredCount > 0)
        {
            _logger.LogInformation(
                "[Username Change Cleanup] Expired {Count} approval tokens",
                expiredCount);
        }
        else
        {
            _logger.LogDebug("[Username Change Cleanup] No expired approval tokens");
        }
    }
}
