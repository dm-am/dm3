using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Inactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the inactive game warnings, freezes and closures on a schedule.
/// </summary>
/// <remarks>
/// The thresholds belong to <see cref="IGameInactivityProcessor" />; this only
/// decides how often to ask.
/// </remarks>
internal class GameInactivityService(IServiceProvider serviceProvider, ILogger<GameInactivityService> logger)
    : PeriodicHostedService(serviceProvider, logger)
{
    /// <inheritdoc />
    protected override string Tag => "[Game Inactivity]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(6);

    /// <inheritdoc />
    protected override TimeSpan StartupDelay => TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var processor = scope.GetRequiredService<IGameInactivityProcessor>();

        await processor.WarnInactiveGamesAsync(cancellationToken);
        await processor.FreezeWarnedGamesAsync(cancellationToken);
        await processor.WarnFrozenGamesAsync(cancellationToken);
        await processor.CloseFrozenGamesAsync(cancellationToken);
    }
}
