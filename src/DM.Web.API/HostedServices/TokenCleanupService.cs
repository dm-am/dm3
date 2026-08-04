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
/// Background service that periodically cleans up expired and removed tokens from the database
/// </summary>
internal class TokenCleanupService : PeriodicHostedService
{
    private readonly ILogger<TokenCleanupService> _logger;

    public TokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<TokenCleanupService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Token Cleanup]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(24);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Token Cleanup] Starting token cleanup");

        var dbContext = scope.GetRequiredService<DmDbContext>();

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7);

        var deletedCount = await dbContext.Tokens
            .Where(t => t.IsRemoved || t.CreatedUtc < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("[Token Cleanup] Deleted {Count} expired or removed tokens (older than {CutoffDate})",
                deletedCount, cutoffDate);
        }
        else
        {
            _logger.LogDebug("[Token Cleanup] No tokens to clean up");
        }
    }
}
