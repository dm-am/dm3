using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Forum.Features.Topics;
using Microsoft.Extensions.Logging;

namespace DM.Domain.Forum.Features.Digests;

/// <inheritdoc />
internal class PeriodDigestProcessor : IPeriodDigestProcessor
{
    /// <summary>Well-known migration-seeded board the digests are posted to.</summary>
    private static readonly Guid NewsBoardId = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static readonly string[] MonthsGenitive =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря",
    ];

    private readonly IPeriodDigestRepository _digestRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IUnreadCountersRepository _unreadCounters;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly ILogger<PeriodDigestProcessor> _logger;

    public PeriodDigestProcessor(
        IPeriodDigestRepository digestRepository,
        ITopicRepository topicRepository,
        IUnreadCountersRepository unreadCounters,
        IEventProducer eventProducer,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        ILogger<PeriodDigestProcessor> logger)
    {
        _digestRepository = digestRepository;
        _topicRepository = topicRepository;
        _unreadCounters = unreadCounters;
        _eventProducer = eventProducer;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> EnsureClosedPeriodsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var created = new List<string>();

        // Last closed calendar month.
        var lastMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1);
        var monthly = await EnsureDigest(lastMonth.Year, lastMonth.Month, cancellationToken);
        if (monthly != null)
        {
            created.Add(monthly);
        }

        // Last closed year, generated separately from the December digest.
        var yearly = await EnsureDigest(now.Year - 1, null, cancellationToken);
        if (yearly != null)
        {
            created.Add(yearly);
        }

        return created;
    }

    /// <summary>
    /// Creates the digest of one period unless it is already there.
    /// </summary>
    /// <returns>Title of the topic created, or null when nothing was.</returns>
    private async Task<string?> EnsureDigest(int year, int? month, CancellationToken cancellationToken)
    {
        if (await _digestRepository.DigestExists(year, month, cancellationToken))
        {
            return null;
        }

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

        // Markers first, row second, commit once the digest is claimed. The claim
        // is what the whole pass hangs on, so the reservation is taken before it
        // and given back on the way out of a run that loses.
        var topicId = _guidFactory.Create();
        await using var counters = await _unreadCounters.ReserveAsync(
            UnreadMarker.UnderParent(topicId, NewsBoardId, UnreadEntryType.Message));

        // The period is taken before the topic is written, so the run that loses
        // the race has nothing to take back. Claimed the other way round, the loser
        // had to delete a topic the board was already pointing at - a delete the
        // board's own reference refuses - and left the board's topic count one
        // too high whichever way that went.
        var claimed = await _digestRepository.TryClaimPeriod(
            topicId, year, month, _dateTimeProvider.Now, cancellationToken);
        if (!claimed)
        {
            // Said out loud, because the answer is the same one "the digest was
            // already there" gives, and the two mean different things: this is a
            // second instance that got to the marker first, and a run that keeps
            // reporting it is a run racing itself.
            _logger.LogInformation(
                "[Period Digest] {Title} was claimed by another instance; nothing was created " +
                "for it here", title);
            return null;
        }

        // Through the normal repository path, so numbering and side effects stay
        // correct.
        //
        // The claim is committed and the topic is not, so a creation that refuses -
        // a deadlock, exhausted retries, the host shutting down - would leave the
        // period marked as generated with no topic under it, and a digest is
        // generated once and never again. The claim is therefore given back before
        // the failure leaves this method, and the next pass makes an honest second
        // attempt. The release itself refuses to fire once the topic exists, so a
        // refusal thrown after the creation committed leaves the period claimed
        // rather than letting the next pass post the digest a second time.
        Topic topic;
        try
        {
            topic = await _topicRepository.Create(
                new CreateTopicEntity { TopicId = topicId, Title = title, Text = string.Empty },
                SystemUser.Id,
                NewsBoardId,
                cancellationToken);
        }
        catch
        {
            // Not under the caller's token: the shutdown that cancelled the creation
            // is exactly the case this has to survive.
            try
            {
                var released = await _digestRepository.ReleasePeriod(topicId, CancellationToken.None);
                if (released == 0)
                {
                    // The topic is there: the creation refused after committing it,
                    // which is what the release condition is for. Nothing is lost,
                    // and the pass that follows must not treat the period as free.
                    _logger.LogWarning(
                        "[Period Digest] {Title} was written before its creation refused; " +
                        "the period stays claimed", title);
                }
            }
            catch (Exception releaseFailure)
            {
                // The creation failure is what the caller has to see; a period left
                // claimed is said out loud instead, because nothing else will ever
                // report it and the digest it blocks is gone for good.
                _logger.LogError(releaseFailure,
                    "[Period Digest] {Title} could not be created and its period could not be " +
                    "released either; no digest will be generated for it", title);
            }

            throw;
        }

        counters.Commit();

        // The topic was created with today's date and the board's summary written
        // from it; backdating it to the close of its period makes that summary
        // wrong, so the board is recomputed by the repository that owns what the
        // summary means.
        await _digestRepository.DateTopicAtPeriodEnd(topic.Id, periodEndUtc, cancellationToken);
        await _topicRepository.RefreshBoardSummary(NewsBoardId, cancellationToken);

        // Post-commit side effect of the normal topic-creation path: the NewTopic
        // event is what the notification dispatcher reads. Search is not on this
        // road at all - the text column carries its own generated vector, written
        // by the database with the row.
        await _eventProducer.SendAsync(EventType.NewTopic, topic.Id);

        return title;
    }
}
