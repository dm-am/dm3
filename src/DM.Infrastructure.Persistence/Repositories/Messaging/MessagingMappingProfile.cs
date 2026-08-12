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
            // Live links only, spelled out. The global soft-delete filter already
            // drops removed links from this projection, so the predicate changes
            // no read today; it is written here because the rule that reads a chat
            // of two as private correspondence counts exactly these people, and
            // that count must not depend on a filter declared elsewhere.
            .ForMember(d => d.Participants, s => s.MapFrom(c => c.UserLinks
                .Where(l => !l.IsRemoved)
                .Select(l => l.User)))
            .ForMember(d => d.UnreadMessagesCount, opt => opt.Ignore())
            .ForMember(d => d.TotalMessagesCount, opt => opt.Ignore());

        CreateMap<DbMessage, Message>()
            .ForMember(d => d.Id, s => s.MapFrom(m => m.MessageId))
            .ForMember(d => d.ChatId, s => s.MapFrom(m => m.ChatId))
            .ForMember(d => d.ChatType, s => s.MapFrom(m => m.Chat.Type))
            .ForMember(d => d.Likes, s => s.Ignore()) // Likes fetched via EntityType+EntityId pattern
            .ForMember(d => d.ModifiedUtc, opt => opt.Ignore()) // Not stored on the DB entity yet
            .ForMember(d => d.Edits, s => s.MapFrom(m => m.Edits.OrderBy(e => e.ModifiedUtc)));

        CreateMap<DbMessageEdit, MessageEdit>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.MessageEditId));

        CreateMap<DbGlobalChatEvent, GlobalChatEvent>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.GlobalChatEventId))
            .ForMember(d => d.StartsUtc, s => s.MapFrom(e => e.StartsUtc))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(e => e.CreatedUtc))
            .ForMember(d => d.StartedUtc, s => s.MapFrom(e => e.StartedUtc))
            .ForMember(d => d.EndedUtc, s => s.MapFrom(e => e.EndedUtc));

        CreateMap<DbGlobalChatEventParticipant, GlobalChatEventParticipant>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.GlobalChatEventParticipantId))
            .ForMember(d => d.JoinedUtc, s => s.MapFrom(p => p.JoinedUtc));
    }
}
