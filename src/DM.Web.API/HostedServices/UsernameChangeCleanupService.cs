using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that handles expiration of username change requests:
/// 1. Auto-rejects pending requests that moderators haven't reviewed within 7 days
/// 2. Expires approval tokens that users haven't used within 48 hours
/// </summary>
internal class UsernameChangeCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UsernameChangeCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly int _pendingExpirationDays = 7;
    private readonly int _approvalTokenExpirationHours = 48;

    public UsernameChangeCleanupService(
        IServiceProvider serviceProvider,
        ILogger<UsernameChangeCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[Username Change Cleanup] Service started. Will run every {Interval} hour(s). " +
            "Pending requests expire after {PendingDays} days, approval tokens after {TokenHours} hours",
            _cleanupInterval.TotalHours,
            _pendingExpirationDays,
            _approvalTokenExpirationHours);

        using var timer = new PeriodicTimer(_cleanupInterval);

        // Run initial cleanup on startup
        await RunCleanup(stoppingToken);

        // Then run periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await RunCleanup(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Username Change Cleanup] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Username Change Cleanup] Unexpected error in cleanup loop");
                // Continue running despite errors
            }
        }
    }

    private async Task RunCleanup(CancellationToken cancellationToken)
    {
        await ExpirePendingRequests(cancellationToken);
        await ExpireApprovalTokens(cancellationToken);
    }

    /// <summary>
    /// Auto-reject pending requests that moderators haven't reviewed within 7 days
    /// </summary>
    private async Task ExpirePendingRequests(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[Username Change Cleanup] Checking for expired pending requests");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Username Change Cleanup] Error expiring pending requests");
        }
    }

    /// <summary>
    /// Expire approval tokens that users haven't used within 48 hours
    /// </summary>
    private async Task ExpireApprovalTokens(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[Username Change Cleanup] Checking for expired approval tokens");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

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
