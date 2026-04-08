using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <inheritdoc />
internal class GlobalChatEventFactory : IGlobalChatEventFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public GlobalChatEventFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public CreateGlobalChatEventEntity Create(CreateGlobalChatEvent createGlobalChatEvent, Guid userId) => new()
    {
        GlobalChatEventId = _guidFactory.Create(),
        Title = createGlobalChatEvent.Title,
        Description = createGlobalChatEvent.Description ?? string.Empty,
        StartsUtc = createGlobalChatEvent.StartsUtc,
        Duration = createGlobalChatEvent.Duration ?? TimeSpan.FromHours(4), // Default to 4 hours if not specified
        IsOpen = createGlobalChatEvent.IsOpen,
        Status = GlobalChatEventStatus.Scheduled,
        CreatedByUserId = userId,
        CreatedUtc = _dateTimeProvider.Now
    };

    /// <inheritdoc />
    public CreateGlobalChatEventParticipantEntity CreateParticipant(Guid eventId, Guid userId, bool isOrganizer) => new()
    {
        GlobalChatEventParticipantId = _guidFactory.Create(),
        GlobalChatEventId = eventId,
        UserId = userId,
        IsOrganizer = isOrganizer,
        JoinedUtc = _dateTimeProvider.Now
    };
}
