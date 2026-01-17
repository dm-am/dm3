using System;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Messaging;

/// <inheritdoc />
internal class MessageIntentionResolver : IIntentionResolver<MessageIntention, Message>
{
    private static readonly TimeSpan EditTimeLimit = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, MessageIntention intention, Message target) =>
        intention switch
        {
            MessageIntention.Edit => CanEditOrDelete(user, target),
            MessageIntention.Delete => CanEditOrDelete(user, target),
            MessageIntention.Like => user.IsAuthenticated && target.Author?.UserId != user.UserId,
            _ => false
        };

    private static bool CanEditOrDelete(AuthenticatedUser user, Message message)
    {
        if (!user.IsAuthenticated) return false;

        // Moderators can always edit/delete
        if (user.Role is UserRole.Admin or UserRole.SeniorModerator or UserRole.Moderator)
            return true;

        // Author can edit/delete within 15 minutes
        if (message.Author?.UserId != user.UserId) return false;

        var timeSinceCreation = DateTimeOffset.UtcNow - message.CreateDate;
        return timeSinceCreation <= EditTimeLimit;
    }
}
