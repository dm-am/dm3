using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Community;
using TopicEntity = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Auto-creates the "Итоги …" digest topics: one per closed calendar month
/// and one per closed year (separately), in the news board — the modern
/// replacement for the hand-written "итоги" темы of the old site, so the
/// community can comment on each period's results.
///
/// The digest's content is the period's live leaderboards, which the client
/// renders from the statistics API via the PeriodDigestTopics marker (SSOT:
/// board data is computed, never copied into content) — the topic itself
/// carries no text.
///
/// Idempotency: a PeriodDigestTopics marker row per generated digest (plus
/// partial unique indexes as the concurrency backstop) — renaming or editing
/// the topic never causes a duplicate; a topic whose marker failed to land
/// is compensated away immediately. Digest topics are BACKDATED to the
/// moment their period closed, so a catch-up behaves exactly like the real
/// timeline (a yearly digest created in July is dated January 1). Catch-up
/// depth is exactly one period of each kind (the LAST closed month / year):
/// if the service was down over a boundary it heals on the next start,
/// without backfilling history.
/// </summary>
internal class PeriodDigestService : BackgroundService
{
    // Well-known migration-seeded ids (same idiom as GameInactivityProcessor).
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid NewsBoardId = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    // Month-boundary polling: the digest must appear shortly after midnight
    // on the 1st; an hourly check is cheap (one indexed marker query).
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private static readonly string[] MonthsGenitive =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря",
    ];

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PeriodDigestService> _logger;

    public PeriodDigestService(
        IServiceProvider serviceProvider,
        ILogger<PeriodDigestService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Period Digest] Service started. Checking every {Interval}h", CheckInterval.TotalHours);

        using var timer = new PeriodicTimer(CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // First pass runs immediately on startup (catch-up), inside
                // the same guard as the periodic ones: a transient failure
                // (e.g. the database not being up yet) must not crash the
                // service — and with it the host.
                await EnsureDigests(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Period Digest] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Period Digest] Unexpected error in digest loop");
                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task EnsureDigests(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var topicRepository = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
        var unreadCountersRepository = scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>();
        var eventProducer = scope.ServiceProvider.GetRequiredService<IInvokedEventProducer>();
        var now = DateTimeOffset.UtcNow;

        // Last closed calendar month.
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var lastMonth = monthStart.AddMonths(-1);
        await EnsureDigest(
            dbContext, topicRepository, unreadCountersRepository, eventProducer,
            lastMonth.Year, lastMonth.Month, cancellationToken);

        // Last closed year (generated separately from the December digest).
        await EnsureDigest(
            dbContext, topicRepository, unreadCountersRepository, eventProducer,
            now.Year - 1, null, cancellationToken);
    }

    private async Task EnsureDigest(
        DmDbContext dbContext,
        ITopicRepository topicRepository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer eventProducer,
        int year,
        int? month,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.PeriodDigestTopics
            .AnyAsync(d => d.Year == year && d.Month == month, cancellationToken);
        if (exists) return;

        var title = month.HasValue
            ? $"Итоги {MonthsGenitive[month.Value - 1]} {year} года"
            : $"Итоги {year} года";

        // A digest topic lives at the moment its period CLOSED (00:00 on the
        // 1st of the next month / on January 1), exactly as if it had been
        // posted right then: a caught-up yearly digest created in July is
        // dated January 1 and sits deep in the news history instead of
        // popping up as a fresh topic.
        var periodEndUtc = month.HasValue
            ? new DateTimeOffset(year, month.Value, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1)
            : new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // The topic goes through the normal repository path so numbering and
        // side effects stay correct. The repository shares this scope's
        // DbContext but commits immediately inside Create, so the topic is
        // durable before the backdate runs. Safety comes from ordering: the
        // backdate + idempotency marker then commit atomically in ONE
        // SaveChanges; if that fails, the compensation below removes the
        // orphaned topic so the next tick can retry cleanly. (A wrapping
        // transaction would also work now, but with EnableRetryOnFailure it
        // needs an execution strategy — the compensation is simpler and
        // covers the same failure.)
        var topic = await topicRepository.Create(
            new CreateTopicEntity { Title = title, Text = string.Empty },
            SystemUserId,
            NewsBoardId,
            cancellationToken);

        try
        {
            // Backdate the topic to the period close; the marker rides in
            // the same SaveChanges (single atomic commit).
            var dbTopic = await dbContext.Set<TopicEntity>()
                .FirstAsync(t => t.TopicId == topic.Id, cancellationToken);
            dbTopic.CreatedUtc = periodEndUtc;

            dbContext.PeriodDigestTopics.Add(new PeriodDigestTopic
            {
                PeriodDigestTopicId = Guid.NewGuid(),
                Year = year,
                Month = month,
                TopicId = topic.Id,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Unique-index race with another instance, or a marker failure.
            // Compensate: drop the just-created topic (its marker never
            // landed), so no duplicate digest ever survives.
            dbContext.ChangeTracker.Clear();
            await dbContext.Set<TopicEntity>()
                .Where(t => t.TopicId == topic.Id)
                .ExecuteDeleteAsync(cancellationToken);
            _logger.LogInformation(ex, "[Period Digest] Digest \"{Title}\" already created concurrently", title);
            return;
        }

        // The repository stamped the board's denormalized last-topic fields
        // with the (now backdated) digest. Recompute them from the actual
        // freshest topic with a direct UPDATE — set-based, immune to
        // whatever the context has tracked.
        var latest = await dbContext.Set<TopicEntity>()
            .Where(t => t.BoardId == NewsBoardId && !t.IsRemoved)
            .OrderByDescending(t => t.CreatedUtc)
            .Select(t => new
            {
                t.TopicId,
                t.TopicNumber,
                t.Title,
                t.AuthorId,
                t.CreatedUtc,
            })
            .FirstAsync(cancellationToken);
        await dbContext.Boards
            .Where(b => b.BoardId == NewsBoardId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.LastTopicId, latest.TopicId)
                .SetProperty(b => b.LastTopicNumber, latest.TopicNumber)
                .SetProperty(b => b.LastTopicTitle, latest.Title)
                .SetProperty(b => b.LastTopicAuthorId, latest.AuthorId)
                .SetProperty(b => b.LastTopicCreatedUtc, latest.CreatedUtc),
                cancellationToken);

        // Post-commit side effects of the normal topic-creation path
        // (mirrors TopicService.CreateAsync): the NewTopic event feeds the
        // search indexer and realtime, the unread base row makes comment
        // unread tracking work for the digest.
        await Task.WhenAll(
            eventProducer.SendAsync(EventType.NewTopic, topic.Id),
            unreadCountersRepository.CreateAsync(topic.Id, NewsBoardId, UnreadEntryType.Message));

        _logger.LogInformation("[Period Digest] Created digest topic \"{Title}\"", title);
    }
}
