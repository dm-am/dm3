using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Messaging;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;

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
    public GlobalChatEvent Create(CreateGlobalChatEvent createGlobalChatEvent, Guid userId) => new()
    {
        GlobalChatEventId = _guidFactory.Create(),
        Title = createGlobalChatEvent.Title,
        Description = createGlobalChatEvent.Description ?? string.Empty,
        StartsAtUtc = createGlobalChatEvent.StartsAt,
        Duration = createGlobalChatEvent.Duration,
        IsOpen = createGlobalChatEvent.IsOpen,
        Status = GlobalChatEventStatus.Scheduled,
        CreatedByUserId = userId,
        CreatedAtUtc = _dateTimeProvider.Now
    };

    /// <inheritdoc />
    public GlobalChatEventParticipant CreateParticipant(Guid eventId, Guid userId, bool isOrganizer) => new()
    {
        GlobalChatEventParticipantId = _guidFactory.Create(),
        GlobalChatEventId = eventId,
        UserId = userId,
        IsOrganizer = isOrganizer,
        JoinedAtUtc = _dateTimeProvider.Now
    };
}
