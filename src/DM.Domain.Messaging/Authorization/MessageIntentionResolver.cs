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

    /// <summary>
    /// Creates a new instance of <see cref="MessageIntentionResolver"/>
    /// </summary>
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
            MessageIntention.Edit => CanEditOrDelete(user, target),
            MessageIntention.Delete => CanEditOrDelete(user, target),
            MessageIntention.Like => user.IsAuthenticated && target.Author?.UserId != user.UserId,
            _ => false
        };

    private bool CanEditOrDelete(IAuthorizationSubject user, Message message)
    {
        if (!user.IsAuthenticated) return false;

        // Moderators can always edit/delete
        if (user.Role is UserRole.Admin or UserRole.SeniorModerator or UserRole.Moderator)
            return true;

        // Author can edit/delete within configured time limit
        if (message.Author?.UserId != user.UserId) return false;

        var timeSinceCreation = _dateTimeProvider.Now - message.CreatedUtc;
        return timeSinceCreation <= _editTimeLimit;
    }
}
