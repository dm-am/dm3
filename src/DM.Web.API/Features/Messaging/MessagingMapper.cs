using DM.Domain.Messaging.Authorization;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
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

/// <summary>
/// Compile-time mapper for chats and messages. The text carrier depends on the
/// chat type, so the render-context envelope is an explicit step of <see cref="ToMessage"/>.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
public partial class MessagingMapper
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
    /// listing does. What the viewer may do in it is the domain's answer,
    /// composed by MessagingApiService on the reads of one chat.
    /// </summary>
    [MapProperty(nameof(DtoChat.LastMessage), nameof(ApiChat.LastMessage), Use = nameof(ToMessage))]
    [MapperIgnoreTarget(nameof(ApiChat.Viewer))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ApiChat ToChat(DtoChat chat);

    /// <summary>
    /// Domain message to the API one. The sender owns it: the envelope lets the
    /// converter honor the sender's AuthorEdit and downgrade anyone else's to Display.
    /// </summary>
    public ApiMessage ToMessage(DtoMessage message)
    {
        if (message == null)
        {
            return null!;
        }

        var result = ToMessageCore(message);
        result.Text = ToMessageText(message);
        result.Text?.BindAuthor(message.Author?.UserId,
            intentions => intentions.IsAllowed(MessageIntention.Edit, message));
        return result;
    }


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

    // The surface comes with the chat type: each carrier names its own.
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
