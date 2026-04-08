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
        CreateMap<DtoMessageEdit, ApiMessageEdit>();
        CreateMap<DtoMessage, ApiMessage>();
        CreateMap<ApiMessage, CreateMessage>()
            .ForMember(d => d.ChatId, opt => opt.Ignore());

        CreateMap<ApiMessage, UpdateMessage>()
            .ForMember(d => d.MessageId, opt => opt.Ignore());

        CreateMap<ApiCreateChat, ServiceCreateChat>();
        CreateMap<ApiUpdateChat, ServiceUpdateChat>()
            .ForMember(d => d.ChatId, opt => opt.Ignore());
    }
}
