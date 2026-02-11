using System.Linq;
using AutoMapper;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;
using DbMessage = DM.Services.DataAccess.BusinessObjects.Messaging.Message;
using DbMessageEdit = DM.Services.DataAccess.BusinessObjects.Messaging.MessageEdit;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <inheritdoc />
internal class MessagingProfile : Profile
{
    /// <inheritdoc />
    public MessagingProfile()
    {
        CreateMap<DbConversation, Conversation>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.ConversationId))
            .ForMember(d => d.Participants, s => s.MapFrom(c => c.UserLinks
                .Select(l => l.User)));

        CreateMap<DbMessage, Message>()
            .ForMember(d => d.Id, s => s.MapFrom(m => m.MessageId))
            .ForMember(d => d.Likes, s => s.Ignore()) // Likes fetched via EntityType+EntityId pattern
            .ForMember(d => d.Edits, s => s.MapFrom(m => m.Edits.OrderBy(e => e.EditedAtUtc)));

        CreateMap<DbMessageEdit, MessageEdit>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.MessageEditId));
    }
}