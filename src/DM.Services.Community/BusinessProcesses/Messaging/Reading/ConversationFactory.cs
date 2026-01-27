using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Implementation;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;
using DbConversationLink = DM.Services.DataAccess.BusinessObjects.Messaging.UserConversationLink;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <inheritdoc />
internal class ConversationFactory : IConversationFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public ConversationFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public (DbConversation conversation, IEnumerable<DbConversationLink>) CreateVisavi(Guid userId, Guid visaviId)
    {
        var conversationId = _guidFactory.Create();
        var conversation = new DbConversation
        {
            ConversationId = conversationId,
            LastMessageId = null,
            Visavi = true
        };
        var links = new[] {userId, visaviId}
            .Distinct()
            .Select(id => new DbConversationLink
            {
                UserConversationLinkId = _guidFactory.Create(),
                ConversationId = conversationId,
                UserId = id,
                IsRemoved = false
            });

        return (conversation, links);
    }

    /// <inheritdoc />
    public (DbConversation conversation, IEnumerable<DbConversationLink>) CreateGroup(string title, IEnumerable<Guid> participantIds)
    {
        var conversationId = _guidFactory.Create();
        var conversation = new DbConversation
        {
            ConversationId = conversationId,
            LastMessageId = null,
            Visavi = false,
            Title = title
        };
        var links = participantIds
            .Distinct()
            .Select(id => new DbConversationLink
            {
                UserConversationLinkId = _guidFactory.Create(),
                ConversationId = conversationId,
                UserId = id,
                IsRemoved = false
            });

        return (conversation, links);
    }
}