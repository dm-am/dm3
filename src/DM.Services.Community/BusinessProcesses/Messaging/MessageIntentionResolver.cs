using System;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Community.Configuration;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using Microsoft.Extensions.Options;

namespace DM.Services.Community.BusinessProcesses.Messaging;

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
    public bool IsAllowed(AuthenticatedUser user, MessageIntention intention, Message target) =>
        intention switch
        {
            MessageIntention.Edit => CanEditOrDelete(user, target),
            MessageIntention.Delete => CanEditOrDelete(user, target),
            MessageIntention.Like => user.IsAuthenticated && target.Author?.UserId != user.UserId,
            _ => false
        };

    private bool CanEditOrDelete(AuthenticatedUser user, Message message)
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
