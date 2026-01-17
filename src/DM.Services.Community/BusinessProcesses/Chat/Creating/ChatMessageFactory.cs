using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Common;

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
    public ChatMessage Create(CreateChatMessage createChatMessage, Guid userId)
    {
        return new ChatMessage
        {
            ChatMessageId = _guidFactory.Create(),
            CreateDate = _dateTimeProvider.Now,
            UserId = userId,
            Text = createChatMessage.Text.Trim()
        };
    }
}