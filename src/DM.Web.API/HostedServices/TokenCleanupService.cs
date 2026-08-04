using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs token retention on a schedule.
/// </summary>
/// <remarks>
/// What is kept and for how long belongs to
/// <see cref="ITokenCleanupProcessor" />; this only decides how often to ask.
/// </remarks>
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

        var deletedCount = await scope
            .GetRequiredService<ITokenCleanupProcessor>()
            .DeleteStaleAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("[Token Cleanup] Deleted {Count} withdrawn or expired tokens", deletedCount);
        }
        else
        {
            _logger.LogDebug("[Token Cleanup] No tokens to clean up");
        }
    }
}
