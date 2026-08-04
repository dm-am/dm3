using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Popularity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Game.Features.Popularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Runs the popularity recalculation on a schedule.
/// </summary>
/// <remarks>
/// What popularity counts belongs to <see cref="IGamePopularityProcessor" /> and
/// <see cref="IBlogPopularityProcessor" />, which the seeder calls as well; this
/// only decides how often to ask.
/// </remarks>
internal class PopularityScoreService : PeriodicHostedService
{
    private readonly ILogger<PopularityScoreService> _logger;

    public PopularityScoreService(
        IServiceProvider serviceProvider,
        ILogger<PopularityScoreService> logger)
        : base(serviceProvider, logger) => _logger = logger;

    /// <inheritdoc />
    protected override string Tag => "[Popularity Score]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    /// <remarks>
    /// The first calculation happens on the first pass rather than being deferred
    /// to the first tick. It used to be deferred on the strength of a comment
    /// saying WarmupService did it at startup — that phase was deleted with the
    /// duplicated copy of WarmupService it lived in, and nothing noticed, because
    /// a stale score looks exactly like a correct one. The result was that
    /// "популярные игры" and "популярные блоги" served whatever the previous run
    /// had persisted for a full hour after every cold start, and zeroes for that
    /// hour on a freshly reset database.
    ///
    /// The service that owns the calculation owns its first run: the coupling to
    /// a warmup phase in another service is what allowed the gap to open.
    /// </remarks>
    protected override async Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var now = scope.GetRequiredService<IDateTimeProvider>().Now;

        await Recalculate("games", () => scope
            .GetRequiredService<IGamePopularityProcessor>()
            .UpdateScoresAsync(now, cancellationToken));

        await Recalculate("blogs", () => scope
            .GetRequiredService<IBlogPopularityProcessor>()
            .UpdateScoresAsync(now, cancellationToken));
    }

    /// <summary>
    /// One half of the pass, guarded on its own so a failure on the games does
    /// not cost the blogs their recalculation for the whole interval.
    /// </summary>
    private async Task Recalculate(string what, Func<Task<(int Updated, int Total)>> recalculate)
    {
        try
        {
            var (updated, total) = await recalculate();
            if (total == 0)
            {
                _logger.LogDebug("[Popularity Score] No {What} to update", what);
                return;
            }

            _logger.LogInformation("[Popularity Score] Updated {UpdatedCount} of {TotalCount} {What}",
                updated, total, what);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Popularity Score] Error updating {What} scores", what);
        }
    }
}
