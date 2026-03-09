using AutoMapper;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.Messages;
using DtoChat = DM.Domain.Messaging.Features.Chats.Chat;
using DtoMessage = DM.Domain.Messaging.Features.Messages.Message;
using DtoMessageEdit = DM.Domain.Messaging.Features.Messages.MessageEdit;
using ServiceCreateChat = DM.Domain.Messaging.Features.Chats.CreateChat;
using ServiceUpdateChat = DM.Domain.Messaging.Features.Chats.UpdateChat;
using ApiChat = DM.Web.API.Features.Messaging.Chats.Chat;
using ApiCreateChat = DM.Web.API.Features.Messaging.Chats.CreateChat;
using ApiUpdateChat = DM.Web.API.Features.Messaging.Chats.UpdateChat;
using ApiConversation = DM.Web.API.Features.Messaging.Conversations.Conversation;
using ApiCreateConversation = DM.Web.API.Features.Messaging.Conversations.CreateConversation;
using ApiUpdateConversation = DM.Web.API.Features.Messaging.Conversations.UpdateConversation;
using ApiMessage = DM.Web.API.Features.Messaging.Messages.Message;
using ApiMessageEdit = DM.Web.API.Features.Messaging.Messages.MessageEdit;

namespace DM.Web.API.Features.Messaging;

/// <inheritdoc />
internal class MessagingMappingProfile : Profile
{
    /// <inheritdoc />
    public MessagingMappingProfile()
    {
        CreateMap<DtoChat, ApiChat>();
        CreateMap<DtoChat, ApiConversation>();
        CreateMap<DtoMessageEdit, ApiMessageEdit>();
        CreateMap<DtoMessage, ApiMessage>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(m => m.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(m => m.ModifiedUtc))
            .ForMember(d => d.DeletedBy, s => s.MapFrom(m => m.DeletedBy))
            .ForMember(d => d.DeletedAtUtc, s => s.MapFrom(m => m.DeletedAtUtc))
            .ForMember(d => d.Edits, s => s.MapFrom(m => m.Edits));
        CreateMap<ApiMessage, CreateMessage>();
        CreateMap<ApiMessage, UpdateMessage>();

        CreateMap<ApiCreateChat, ServiceCreateChat>();
        CreateMap<ApiUpdateChat, ServiceUpdateChat>();
        CreateMap<ApiCreateConversation, ServiceCreateChat>();
        CreateMap<ApiUpdateConversation, ServiceUpdateChat>();
    }
}
