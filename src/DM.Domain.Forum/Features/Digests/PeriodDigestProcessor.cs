using System;
using System.Collections.Generic;
using System.Globalization;
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
        CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.MonthGenitiveNames;

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
        CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        var created = new List<string>();

        // Last closed calendar month.
        var lastMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1);
        var monthly = await EnsureDigest(lastMonth.Year, lastMonth.Month, ct);
        if (monthly != null)
        {
            created.Add(monthly);
        }

        // Last closed year, generated separately from the December digest.
        var yearly = await EnsureDigest(now.Year - 1, null, ct);
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
    private async Task<string?> EnsureDigest(int year, int? month, CancellationToken ct)
    {
        if (await _digestRepository.DigestExists(year, month, ct))
        {
            return null;
        }

        var title = month.HasValue
            ? $"Итоги {MonthsGenitive[month.Value - 1]} {year} года"
            : $"Итоги {year} года";

        // Dated as if posted the moment its period closed, so a caught-up digest
        // sits in the news history instead of popping up as a fresh topic.
        var periodEndUtc = month.HasValue
            ? new DateTimeOffset(year, month.Value, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1)
            : new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Markers first, row second, commit once the digest is claimed; a run that
        // loses the claim gives the reservation back on the way out.
        var topicId = _guidFactory.Create();
        await using var counters = await _unreadCounters.ReserveAsync(
            UnreadMarker.UnderParent(topicId, NewsBoardId, UnreadEntryType.Message));

        // The period is taken before the topic is written, so the run that loses
        // the race has nothing to take back.
        var claimed = await _digestRepository.TryClaimPeriod(
            topicId, year, month, _dateTimeProvider.Now, ct);
        if (!claimed)
        {
            // Logged: it answers as "already there" does, but means a second
            // instance got to the marker first.
            _logger.LogInformation(
                "[Period Digest] {Title} was claimed by another instance; nothing was created " +
                "for it here", title);
            return null;
        }

        var topic = await CreateDigestTopic(topicId, title, ct);

        counters.Commit();

        // Backdating makes the board's summary, written from today's date, wrong.
        await _digestRepository.DateTopicAtPeriodEnd(topic.Id, periodEndUtc, ct);
        await _topicRepository.RefreshBoardSummary(NewsBoardId, ct);

        // The NewTopic event of the normal creation path, for the notification dispatcher.
        await _eventProducer.SendAsync(EventType.NewTopic, topic.Id);

        return title;
    }

    /// <summary>
    /// Writes the topic of a claimed period, and gives the claim back when the
    /// write refuses.
    /// </summary>
    private async Task<Topic> CreateDigestTopic(Guid topicId, string title, CancellationToken ct)
    {
        // The claim is committed and the topic is not, so a refused creation gives
        // the claim back and the next pass makes an honest second attempt.
        try
        {
            return await _topicRepository.Create(
                new CreateTopicEntity { TopicId = topicId, Title = title, Text = string.Empty },
                SystemUser.Id,
                NewsBoardId,
                ct);
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
                    // The creation refused after committing the topic: the period stays claimed.
                    _logger.LogWarning(
                        "[Period Digest] {Title} was written before its creation refused; " +
                        "the period stays claimed", title);
                }
            }
            catch (Exception releaseFailure)
            {
                // The caller sees the creation failure; a period left claimed is logged,
                // because nothing else reports it and its digest is gone for good.
                _logger.LogError(releaseFailure,
                    "[Period Digest] {Title} could not be created and its period could not be " +
                    "released either; no digest will be generated for it", title);
            }

            throw;
        }
    }
}
