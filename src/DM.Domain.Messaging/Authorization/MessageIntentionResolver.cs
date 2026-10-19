using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;
using DM.Domain.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace DM.Domain.Messaging.Authorization;

/// <inheritdoc />
internal class MessageIntentionResolver : IIntentionResolver<MessageIntention, Message>
{
    private readonly TimeSpan _editTimeLimit;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MessageIntentionResolver(
        IOptions<MessagingConfiguration> options,
        IDateTimeProvider dateTimeProvider)
    {
        _editTimeLimit = TimeSpan.FromMinutes(options.Value.EditTimeoutMinutes);
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, MessageIntention intention, Message target) =>
        intention switch
        {
            // Rewriting publishes text as sending does, so the author's edit in the global
            // chat answers to the ban (ChatIntention.CreateMessage); a moderator's edit is moderation.
            MessageIntention.Edit when target.ChatType == ChatType.Global &&
                                       target.Author?.UserId == user.UserId =>
                CanEditOrDelete(user, target) && user.MaySpeak(),
            MessageIntention.Edit or MessageIntention.Delete => CanEditOrDelete(user, target),
            MessageIntention.Like => user.IsAuthenticated && target.Author?.UserId != user.UserId,
            _ => false
        };

    private bool CanEditOrDelete(IAuthorizationSubject user, Message message)
    {
        if (!user.IsAuthenticated) return false;

        // Moderation reaches the global chat alone: a direct message, a group chat
        // and the chat of a game room are not moderated.
        if (message.ChatType == ChatType.Global &&
            user.Role is UserRole.Admin or UserRole.SeniorModerator or UserRole.Moderator)
            return true;

        if (message.Author?.UserId != user.UserId) return false;

        var timeSinceCreation = _dateTimeProvider.Now - message.CreatedUtc;
        return timeSinceCreation <= _editTimeLimit;
    }
}
