using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Game.Configuration;

namespace DM.Domain.Game.Features.PostPendencies;

/// <inheritdoc />
internal class PendencyReminderProcessor : IPendencyReminderProcessor
{
    private readonly IPostPendencyRepository _repository;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PendencyReminderProcessor(
        IPostPendencyRepository repository,
        IEventProducer eventProducer,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _eventProducer = eventProducer;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<int> SendDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;

        // Claimed - stamped and committed - before a single event goes out. The
        // stamp is what stops the same reminder repeating every pass.
        var claimed = await _repository.ClaimPendingReminders(
            now - PendencyPolicy.FirstReminderAfter,
            now - PendencyPolicy.ReminderInterval,
            now,
            cancellationToken);

        foreach (var pendencyId in claimed)
        {
            await _eventProducer.SendAsync(EventType.RoomPendencyReminder, pendencyId);
        }

        return claimed.Count;
    }
}
