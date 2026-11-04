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
internal class PopularityScoreService(IServiceProvider serviceProvider, ILogger<PopularityScoreService> logger)
    : PeriodicHostedService(serviceProvider, logger)
{
    /// <inheritdoc />
    protected override string Tag => "[Popularity Score]";

    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <inheritdoc />
    /// <remarks>
    /// The first recalculation runs on the first pass, so a cold start does not
    /// serve stale scores for an hour. Games and blogs are guarded apart: a
    /// failure on one does not cost the other its recalculation.
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

    private Task Recalculate(string what, Func<Task<(int Updated, int Total)>> recalculate) =>
        Guarded($"updating {what} scores", async () =>
        {
            var (updated, total) = await recalculate();
            if (total > 0)
            {
                Logger.LogInformation("{Tag} Updated {UpdatedCount} of {TotalCount} {What}",
                    Tag, updated, total, what);
            }
        });
}
