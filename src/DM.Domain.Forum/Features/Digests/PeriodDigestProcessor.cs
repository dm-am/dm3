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
    private readonly ILogger<PeriodDigestProcessor> _logger;

    public PeriodDigestProcessor(
        IPeriodDigestRepository digestRepository,
        ITopicRepository topicRepository,
        IUnreadCountersRepository unreadCounters,
        IEventProducer eventProducer,
        IDateTimeProvider dateTimeProvider,
        ILogger<PeriodDigestProcessor> logger)
    {
        _digestRepository = digestRepository;
        _topicRepository = topicRepository;
        _unreadCounters = unreadCounters;
        _eventProducer = eventProducer;
        _dateTimeProvider = dateTimeProvider;
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

        // Through the normal repository path, so numbering and side effects stay
        // correct. It commits the topic immediately, which is why the marker step
        // below has to be able to take it back.
        var topic = await _topicRepository.Create(
            new CreateTopicEntity { Title = title, Text = string.Empty },
            SystemUser.Id,
            NewsBoardId,
            cancellationToken);

        var recorded = await _digestRepository.TryRecordDigest(
            topic.Id, year, month, periodEndUtc, _dateTimeProvider.Now, cancellationToken);
        if (!recorded)
        {
            // Said out loud, because the answer is the same one "the digest was
            // already there" gives, and the two mean different things: this is a
            // second instance that got to the marker first, and a run that keeps
            // reporting it is a run racing itself.
            _logger.LogInformation(
                "[Period Digest] {Title} was claimed by another instance; the topic just " +
                "created for it has been taken back", title);
            return null;
        }

        await _digestRepository.RefreshLastTopic(NewsBoardId, cancellationToken);

        // Post-commit side effects of the normal topic-creation path (mirrors
        // TopicService.CreateAsync): the NewTopic event feeds the search indexer
        // and realtime, the unread base row makes comment unread tracking work
        // for the digest.
        await Task.WhenAll(
            _eventProducer.SendAsync(EventType.NewTopic, topic.Id),
            _unreadCounters.CreateAsync(topic.Id, NewsBoardId, UnreadEntryType.Message));

        return title;
    }
}
