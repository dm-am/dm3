using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Domain.Personal.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Notifiers.Blog;

/// <summary>
/// Generates notifications when a user is invited to a blog
/// </summary>
internal class BlogInvitationNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    public BlogInvitationNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.BlogInvitationCreated;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var invitationData = await _dbContext.Tokens
            .Where(t => t.TokenId == entityId && !t.IsRemoved && t.EntityId.HasValue)
            .Select(t => new
            {
                t.UserId,
                BlogId = t.EntityId!.Value,
                BlogTitle = t.Blog!.Title,
                InviterId = t.CreatorId ?? t.Blog.AuthorId,
                InviterUsername = t.Blog.Author!.Username,
                t.Type
            })
            .FirstOrDefaultAsync();

        if (invitationData == null)
        {
            yield break;
        }

        var role = invitationData.Type == TokenType.BlogAssistantInvitation ? "Ассистент" : "Читатель";

        // Falls back to the blog author for a token without a creator, as BlogInvitationRepository does.
        yield return new CreateNotification
        {
            UsersInterested = [invitationData.UserId],
            ActorId = invitationData.InviterId,
            Metadata = new
            {
                BlogId = invitationData.BlogId.EncodeToReadable(invitationData.BlogTitle),
                invitationData.BlogTitle,
                invitationData.InviterUsername,
                Role = role
            }
        };
    }
}
