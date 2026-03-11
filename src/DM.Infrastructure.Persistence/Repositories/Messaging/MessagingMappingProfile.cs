using System.Linq;
using AutoMapper;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Domain.Messaging.Features.Messages;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbMessageEdit = DM.Infrastructure.Persistence.Entities.Messaging.MessageEdit;
using DbGlobalChatEvent = DM.Infrastructure.Persistence.Entities.Messaging.GlobalChatEvent;
using DbGlobalChatEventParticipant = DM.Infrastructure.Persistence.Entities.Messaging.GlobalChatEventParticipant;

namespace DM.Infrastructure.Persistence.Repositories.Messaging;

/// <inheritdoc />
internal class MessagingMappingProfile : Profile
{
    /// <inheritdoc />
    public MessagingMappingProfile()
    {
        CreateMap<DbChat, Chat>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.ChatId))
            .ForMember(d => d.Participants, s => s.MapFrom(c => c.UserLinks
                .Select(l => l.User)));

        CreateMap<DbMessage, Message>()
            .ForMember(d => d.Id, s => s.MapFrom(m => m.MessageId))
            .ForMember(d => d.ChatId, s => s.MapFrom(m => m.ChatId))
            .ForMember(d => d.ChatType, s => s.MapFrom(m => m.Chat.Type))
            .ForMember(d => d.Likes, s => s.Ignore()) // Likes fetched via EntityType+EntityId pattern
            .ForMember(d => d.Edits, s => s.MapFrom(m => m.Edits.OrderBy(e => e.EditedAtUtc)));

        CreateMap<DbMessageEdit, MessageEdit>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.MessageEditId));

        CreateMap<DbGlobalChatEvent, GlobalChatEvent>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.GlobalChatEventId))
            .ForMember(d => d.StartsAt, s => s.MapFrom(e => e.StartsAtUtc))
            .ForMember(d => d.CreatedAt, s => s.MapFrom(e => e.CreatedUtc))
            .ForMember(d => d.StartedAt, s => s.MapFrom(e => e.StartedAtUtc))
            .ForMember(d => d.EndedAt, s => s.MapFrom(e => e.EndedAtUtc));

        CreateMap<DbGlobalChatEventParticipant, GlobalChatEventParticipant>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.GlobalChatEventParticipantId))
            .ForMember(d => d.JoinedAt, s => s.MapFrom(p => p.JoinedAtUtc));
    }
}
