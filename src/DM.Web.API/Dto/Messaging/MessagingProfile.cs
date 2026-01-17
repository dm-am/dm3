using AutoMapper;
using DM.Services.Community.BusinessProcesses.Chat.Creating;
using DM.Services.Community.BusinessProcesses.Messaging.Creating;
using DM.Services.Community.BusinessProcesses.Messaging.Updating;
using DtoConversation = DM.Services.Community.BusinessProcesses.Messaging.Reading.Conversation;
using DtoMessage = DM.Services.Community.BusinessProcesses.Messaging.Reading.Message;
using DtoChatMessage = DM.Services.Community.BusinessProcesses.Chat.Reading.ChatMessage;

namespace DM.Web.API.Dto.Messaging;

/// <inheritdoc />
internal class MessagingProfile : Profile
{
    /// <inheritdoc />
    public MessagingProfile()
    {
        CreateMap<DtoConversation, Conversation>();
        CreateMap<DtoMessage, Message>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(m => m.CreateDate))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(m => m.LastUpdateDate));
        CreateMap<Message, CreateMessage>();
        CreateMap<Message, UpdateMessage>();

        CreateMap<DtoChatMessage, ChatMessage>()
            .ForMember(dest => dest.CreatedUtc, opt => opt.MapFrom(src => src.CreateDate))
            .ForMember(dest => dest.ModifiedUtc, opt => opt.MapFrom(src => src.LastUpdateDate))
            .ForMember(dest => dest.Author, opt => opt.MapFrom(src => src.Author))
            .ForMember(dest => dest.Likes, opt => opt.MapFrom(src => src.Likes));
        CreateMap<ChatMessage, CreateChatMessage>();
    }
}