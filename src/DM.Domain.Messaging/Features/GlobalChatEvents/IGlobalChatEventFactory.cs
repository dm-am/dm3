using System;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

public interface IGlobalChatEventFactory
{
    CreateGlobalChatEventEntity Create(CreateGlobalChatEvent createGlobalChatEvent, Guid userId);

    CreateGlobalChatEventParticipantEntity CreateParticipant(Guid eventId, Guid userId, bool isOrganizer);
}
