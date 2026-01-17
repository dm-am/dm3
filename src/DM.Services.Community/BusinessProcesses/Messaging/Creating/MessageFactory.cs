using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Messaging;

namespace DM.Services.Community.BusinessProcesses.Messaging.Creating;

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
    public Message Create(CreateMessage createMessage, Guid userId) => new()
    {
        MessageId = _guidFactory.Create(),
        ConversationId = createMessage.ConversationId,
        CreateDate = _dateTimeProvider.Now,
        UserId = userId,
        Text = createMessage.Text,
        IsRemoved = false
    };
}