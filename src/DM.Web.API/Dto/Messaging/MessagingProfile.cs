using AutoMapper;
using DM.Services.Community.BusinessProcesses.Messaging.Creating;
using DM.Services.Community.BusinessProcesses.Messaging.Updating;
using DtoConversation = DM.Services.Community.BusinessProcesses.Messaging.Reading.Conversation;
using DtoMessage = DM.Services.Community.BusinessProcesses.Messaging.Reading.Message;
using DtoMessageEdit = DM.Services.Community.BusinessProcesses.Messaging.Reading.MessageEdit;
using ServiceCreateConversation = DM.Services.Community.BusinessProcesses.Messaging.Creating.CreateConversation;
using ServiceUpdateConversation = DM.Services.Community.BusinessProcesses.Messaging.Updating.UpdateConversation;

namespace DM.Web.API.Dto.Messaging;

/// <inheritdoc />
internal class MessagingProfile : Profile
{
    /// <inheritdoc />
    public MessagingProfile()
    {
        CreateMap<DtoConversation, Conversation>();
        CreateMap<DtoMessageEdit, MessageEdit>();
        CreateMap<DtoMessage, Message>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(m => m.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(m => m.ModifiedUtc))
            .ForMember(d => d.DeletedBy, s => s.MapFrom(m => m.DeletedBy))
            .ForMember(d => d.DeletedAtUtc, s => s.MapFrom(m => m.DeletedAtUtc))
            .ForMember(d => d.Edits, s => s.MapFrom(m => m.Edits));
        CreateMap<Message, CreateMessage>();
        CreateMap<Message, UpdateMessage>();

        CreateMap<CreateConversation, ServiceCreateConversation>();
        CreateMap<UpdateConversation, ServiceUpdateConversation>();
    }
}
