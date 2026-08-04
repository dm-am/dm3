using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Inactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background service that processes inactive games:
/// - Warns active games with no posts for 1 month
/// - Freezes warned games after 1 week
/// - Warns frozen games after 3 months
/// - Closes frozen games 1 week after warning
/// </summary>
internal class GameInactivityService : PeriodicHostedService
{
    private readonly ILogger<GameInactivityService> _logger;

    public GameInactivityService(
        IServiceProvider serviceProvider,
        ILogger<GameInactivityService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Game Inactivity]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(6);

    /// <inheritdoc />
    /// <remarks>Lets the rest of the host settle before the first pass.</remarks>
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Game Inactivity] Starting inactivity processing");

        var processor = scope.GetRequiredService<IGameInactivityProcessor>();

        // Process in order: warnings first, then actions
        await processor.WarnInactiveGamesAsync(cancellationToken);
        await processor.FreezeWarnedGamesAsync(cancellationToken);
        await processor.WarnFrozenGamesAsync(cancellationToken);
        await processor.CloseFrozenGamesAsync(cancellationToken);

        _logger.LogDebug("[Game Inactivity] Completed inactivity processing");
    }
}
