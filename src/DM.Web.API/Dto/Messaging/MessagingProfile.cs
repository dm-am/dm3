using AutoMapper;
using DM.Services.Community.BusinessProcesses.Chat.Creating;
using DM.Services.Community.BusinessProcesses.Messaging.Creating;
using DM.Services.Community.BusinessProcesses.Messaging.Updating;
using DtoConversation = DM.Services.Community.BusinessProcesses.Messaging.Reading.Conversation;
using DtoMessage = DM.Services.Community.BusinessProcesses.Messaging.Reading.Message;
using DtoChatMessage = DM.Services.Community.BusinessProcesses.Chat.Reading.ChatMessage;
using DtoChatMessageEdit = DM.Services.Community.BusinessProcesses.Chat.Reading.ChatMessageEdit;
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
        CreateMap<DtoMessage, Message>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(m => m.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(m => m.ModifiedUtc));
        CreateMap<Message, CreateMessage>();
        CreateMap<Message, UpdateMessage>();

        CreateMap<CreateConversation, ServiceCreateConversation>();
        CreateMap<UpdateConversation, ServiceUpdateConversation>();

        CreateMap<DtoChatMessageEdit, ChatMessageEdit>();
        CreateMap<DtoChatMessage, ChatMessage>()
            .ForMember(dest => dest.CreatedUtc, opt => opt.MapFrom(src => src.CreatedUtc))
            .ForMember(dest => dest.ModifiedUtc, opt => opt.MapFrom(src => src.ModifiedUtc))
            .ForMember(dest => dest.Author, opt => opt.MapFrom(src => src.Author))
            .ForMember(dest => dest.Likes, opt => opt.MapFrom(src => src.Likes))
            .ForMember(dest => dest.DeletedBy, opt => opt.MapFrom(src => src.DeletedBy))
            .ForMember(dest => dest.DeletedAtUtc, opt => opt.MapFrom(src => src.DeletedAtUtc))
            .ForMember(dest => dest.Edits, opt => opt.MapFrom(src => src.Edits));
        CreateMap<ChatMessage, CreateChatMessage>();
    }
}