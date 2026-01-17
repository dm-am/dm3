using System;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Chat;

/// <inheritdoc />
internal class ChatIntentionResolver :
    IIntentionResolver<ChatIntention>,
    IIntentionResolver<ChatIntention, ChatMessage>
{
    private static readonly TimeSpan EditTimeLimit = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, ChatIntention intention) => intention switch
    {
        ChatIntention.CreateMessage => user.IsAuthenticated,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, ChatIntention intention, ChatMessage target) =>
        intention switch
        {
            ChatIntention.EditMessage => CanEditOrDelete(user, target),
            ChatIntention.DeleteMessage => CanEditOrDelete(user, target),
            ChatIntention.LikeMessage => user.IsAuthenticated,
            _ => false
        };

    private static bool CanEditOrDelete(AuthenticatedUser user, ChatMessage message)
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