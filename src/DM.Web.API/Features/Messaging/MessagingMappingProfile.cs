using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.Messages;
using DM.Web.API.Shared.BbRendering;
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
        // Message text carries a surface-specific BbText picked by
        // MessageTextResolver: global chat -> safe-image GlobalChatBbText,
        // direct/group -> DirectMessageBbText (no [mod]/[private]),
        // game-room and the rest -> default CommonBbText.
        CreateMap<DtoMessage, ApiMessage>()
            .ForMember(d => d.Text, opt => opt.MapFrom<MessageTextResolver>())
            .AfterMap((src, dest) =>
            {
                // Owner of a message is its sender. Populate the render-context
                // envelope so the JSON converter honors the sender's AuthorEdit
                // round-trip and downgrades any other viewer's author_edit
                // request to permission-filtered Display. getMessageForEdit
                // (GET messages/{id}) sends X-Dm-Audience: author_edit for both
                // global chat and direct/group messages. The GlobalChatMessage
                // and DirectMessage surfaces allow neither [mod] nor [private],
                // so this is round-trip integrity rather than a leak vector.
                if (dest.Text is not null)
                    dest.Text.Context = new RenderContextEnvelope
                    {
                        Surface = dest.Text.Surface,
                        PostAuthorUserId = src.Author?.UserId
                    };
            });
        CreateMap<ApiMessage, CreateMessage>()
            .ForMember(d => d.ChatId, opt => opt.Ignore());

        CreateMap<ApiMessage, UpdateMessage>()
            .ForMember(d => d.MessageId, opt => opt.Ignore());

        CreateMap<ApiCreateChat, ServiceCreateChat>();
        CreateMap<ApiUpdateChat, ServiceUpdateChat>()
            .ForMember(d => d.ChatId, opt => opt.Ignore());
    }

    /// <summary>
    /// Picks the correct BbText surface for a message's text: global chat
    /// messages get the safe-image <see cref="GlobalChatBbText"/>, direct and
    /// group chats get <see cref="DirectMessageBbText"/> (no [mod]/[private]),
    /// and everything else (game-room chat) keeps the default
    /// <see cref="CommonBbText"/> (Comment surface).
    /// </summary>
    private sealed class MessageTextResolver : IValueResolver<DtoMessage, ApiMessage, CommonBbText>
    {
        public CommonBbText Resolve(
            DtoMessage source, ApiMessage destination, CommonBbText destMember, ResolutionContext context)
        {
            if (source.Text == null)
            {
                return null!;
            }

            return source.ChatType switch
            {
                ChatType.Global => new GlobalChatBbText { Value = source.Text },
                ChatType.Direct or ChatType.Group => new DirectMessageBbText { Value = source.Text },
                _ => new CommonBbText { Value = source.Text }
            };
        }
    }
}
