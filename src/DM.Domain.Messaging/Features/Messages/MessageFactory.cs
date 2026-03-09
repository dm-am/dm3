using System;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Messaging.Features.Messages;

/// <inheritdoc />
internal class MessageFactory : IMessageFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public MessageFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public CreateMessageEntity Create(CreateMessage createMessage, Guid userId) => new()
    {
        MessageId = _guidFactory.Create(),
        ChatId = createMessage.ChatId,
        CreatedUtc = _dateTimeProvider.Now,
        UserId = userId,
        Text = createMessage.Text,
        IsRemoved = false
    };
}
