using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Messaging;

namespace DM.Services.Community.BusinessProcesses.Chat.Creating;

/// <inheritdoc />
internal class ChatMessageFactory : IChatMessageFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public ChatMessageFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Message Create(CreateChatMessage createChatMessage, Guid userId)
    {
        return new Message
        {
            MessageId = _guidFactory.Create(),
            ConversationId = Message.GlobalChatId,
            CreatedUtc = _dateTimeProvider.Now,
            UserId = userId,
            Text = createChatMessage.Text.Trim()
        };
    }
}
