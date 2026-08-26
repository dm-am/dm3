using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
using DtoChat = DM.Domain.Messaging.Features.Chats.Chat;
using DtoMessage = DM.Domain.Messaging.Features.Messages.Message;
using DtoMessageEdit = DM.Domain.Messaging.Features.Messages.MessageEdit;
using ServiceCreateChat = DM.Domain.Messaging.Features.Chats.CreateChat;
using ServiceUpdateChat = DM.Domain.Messaging.Features.Chats.UpdateChat;
using ServiceCreateMessage = DM.Domain.Messaging.Features.Messages.CreateMessage;
using ServiceUpdateMessage = DM.Domain.Messaging.Features.Messages.UpdateMessage;
using ApiChat = DM.Web.API.Features.Messaging.Chats.Chat;
using ApiCreateChat = DM.Web.API.Features.Messaging.Chats.CreateChat;
using ApiUpdateChat = DM.Web.API.Features.Messaging.Chats.UpdateChat;
using ApiMessage = DM.Web.API.Features.Messaging.Messages.Message;
using ApiMessageEdit = DM.Web.API.Features.Messaging.Messages.MessageEdit;

namespace DM.Web.API.Features.Messaging;

/// <summary>
/// Compile-time mapper for chats and messages. The Mapperly counterpart of
/// the messaging profile: the surface-specific text carrier is picked by a
/// plain method instead of a resolver, and the render-context envelope is an
/// explicit step of <see cref="ToMessage"/>.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
internal partial class MessagingMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public MessagingMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain chat to the API one. The last message goes through
    /// <see cref="ToMessage"/> so it carries the same envelope a message
    /// listing does.
    /// </summary>
    [MapProperty(nameof(DtoChat.LastMessage), nameof(ApiChat.LastMessage), Use = nameof(ToMessage))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ApiChat ToChat(DtoChat chat);

    /// <summary>
    /// Domain message to the API one. The explicit envelope step replaces the
    /// AutoMapper AfterMap: owner of a message is its sender, and the
    /// envelope is what lets the JSON converter honor the sender's AuthorEdit
    /// round-trip (getMessageForEdit sends X-Dm-Audience: author_edit) while
    /// downgrading any other viewer's author_edit request to
    /// permission-filtered Display. The GlobalChatMessage surface allows
    /// [mod] (public on read, so the round-trip is not a leak vector) and no
    /// [private]; the DirectMessage surface allows neither tag.
    /// </summary>
    public ApiMessage ToMessage(DtoMessage message)
    {
        if (message == null)
        {
            return null!;
        }

        var result = ToMessageCore(message);
        result.Text = ToMessageText(message);
        if (result.Text is not null)
        {
            result.Text.Context = new RenderContextEnvelope
            {
                Surface = result.Text.Surface,
                PostAuthorUserId = message.Author?.UserId
            };
        }

        return result;
    }

    /// <summary>
    /// Create body to the write model. ChatId comes from the route, the
    /// service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(ServiceCreateMessage.ChatId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ServiceCreateMessage ToCreateMessage(ApiMessage message);

    /// <summary>
    /// Update body to the write model. MessageId comes from the route, the
    /// service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(ServiceUpdateMessage.MessageId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ServiceUpdateMessage ToUpdateMessage(ApiMessage message);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ServiceCreateChat ToCreateChat(ApiCreateChat chat);

    /// <summary>
    /// Chat update request to the write model. ChatId comes from the route,
    /// the service sets it. Null collections mean "not sent" and must stay
    /// null - an empty list here would read as "remove everybody".
    /// </summary>
    [MapperIgnoreTarget(nameof(ServiceUpdateChat.ChatId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ServiceUpdateChat ToUpdateChat(ApiUpdateChat chat);

    // Text and its envelope are composed in the wrapper - the carrier type
    // depends on the chat type, which is not a member of the API message.
    [MapperIgnoreTarget(nameof(ApiMessage.Text))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ApiMessage ToMessageCore(DtoMessage message);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ApiMessageEdit ToMessageEdit(DtoMessageEdit edit);

    // Message text carries a surface-specific BbText: global chat gets the
    // safe-image GlobalChatBbText, direct/group chats DirectMessageBbText
    // (no [mod]/[private]), game-room and the rest keep the default
    // CommonBbText (Comment surface).
    private static CommonBbText ToMessageText(DtoMessage message)
    {
        if (message.Text == null)
        {
            return null!;
        }

        return message.ChatType switch
        {
            ChatType.Global => new GlobalChatBbText { Value = message.Text },
            ChatType.Direct or ChatType.Group => new DirectMessageBbText { Value = message.Text },
            _ => new CommonBbText { Value = message.Text }
        };
    }
}
