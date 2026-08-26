using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Domain.Messaging.Features.Messages;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbGlobalChatEvent = DM.Infrastructure.Persistence.Entities.Messaging.GlobalChatEvent;
using DbGlobalChatEventParticipant = DM.Infrastructure.Persistence.Entities.Messaging.GlobalChatEventParticipant;

namespace DM.Infrastructure.Persistence.Repositories.Messaging;

/// <summary>
/// Projection formulas for the messaging module. The message shape appears
/// twice - on its own and as a chat's last message - and the two blocks must
/// stay identical. (They could be one spliced expression, like the user
/// formula; nobody has needed a third copy yet.)
/// </summary>
internal static class MessagingMappers
{
    /// <summary>
    /// The one event-participant formula, asked for on its own and nested
    /// under the event.
    /// </summary>
    private static readonly Expression<Func<DbGlobalChatEventParticipant, GlobalChatEventParticipant>>
        ParticipantProjection =
            p => new GlobalChatEventParticipant
            {
                Id = p.GlobalChatEventParticipantId,
                IsOrganizer = p.IsOrganizer,
                JoinedUtc = p.JoinedUtc,
                User = GeneralUserProjections.Projection.Splice(p.User)
            };

    /// <summary>
    /// EF projection to the domain chat. Live participant links only,
    /// spelled out: the global soft-delete filter already drops removed
    /// links, but the rule that reads a chat of two as private
    /// correspondence counts exactly these people, and that count must not
    /// depend on a filter declared elsewhere. Unread and total counters are
    /// filled by the repository.
    /// </summary>
    public static IQueryable<Chat> ProjectToChat(this IQueryable<DbChat> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbChat, Chat>>(c => new Chat
        {
            Id = c.ChatId,
            Type = c.Type,
            Title = c.Title!,
            RoomId = c.RoomId,
            Participants = c.UserLinks
                .Where(l => !l.IsRemoved)
                .Select(l => GeneralUserProjections.Projection.Splice(l.User))
                .ToList(),
            LastMessage = c.LastMessage == null
                ? null!
                : new Message
                {
                    Id = c.LastMessage.MessageId,
                    ChatId = c.LastMessage.ChatId,
                    ChatType = c.Type,
                    CreatedUtc = c.LastMessage.CreatedUtc,
                    Text = c.LastMessage.Text,
                    IsRemoved = c.LastMessage.IsRemoved,
                    DeletedUtc = c.LastMessage.DeletedUtc,
                    GlobalChatEventId = c.LastMessage.GlobalChatEventId,
                    Author = GeneralUserProjections.Projection.Splice(c.LastMessage.Author),
                    DeletedBy = GeneralUserProjections.Projection.Splice(c.LastMessage.DeletedBy),
                    Edits = c.LastMessage.Edits
                        .OrderBy(e => e.ModifiedUtc)
                        .Select(e => new MessageEdit
                        {
                            Id = e.MessageEditId,
                            ModifiedUtc = e.ModifiedUtc,
                            Editor = GeneralUserProjections.Projection.Splice(e.Editor)
                        })
                        .ToList()
                }
        }));

    /// <summary>
    /// EF projection to the domain message. ModifiedUtc is not stored on the
    /// DB entity yet. Likes are absent here on purpose: they live in the
    /// polymorphic Likes table (EntityType + EntityId) with no navigation to
    /// project through, and the repository backfills them for a whole page at
    /// once (MessageRepository.FillLikes) rather than one subquery per row.
    /// </summary>
    public static IQueryable<Message> ProjectToMessage(this IQueryable<DbMessage> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbMessage, Message>>(m => new Message
        {
            Id = m.MessageId,
            ChatId = m.ChatId,
            ChatType = m.Chat.Type,
            CreatedUtc = m.CreatedUtc,
            Text = m.Text,
            IsRemoved = m.IsRemoved,
            DeletedUtc = m.DeletedUtc,
            GlobalChatEventId = m.GlobalChatEventId,
            Author = GeneralUserProjections.Projection.Splice(m.Author),
            DeletedBy = GeneralUserProjections.Projection.Splice(m.DeletedBy),
            Edits = m.Edits
                .OrderBy(e => e.ModifiedUtc)
                .Select(e => new MessageEdit
                {
                    Id = e.MessageEditId,
                    ModifiedUtc = e.ModifiedUtc,
                    Editor = GeneralUserProjections.Projection.Splice(e.Editor)
                })
                .ToList()
        }));

    /// <summary>
    /// EF projection to the domain global chat event
    /// </summary>
    public static IQueryable<GlobalChatEvent> ProjectToGlobalChatEvent(
        this IQueryable<DbGlobalChatEvent> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbGlobalChatEvent, GlobalChatEvent>>(
            e => new GlobalChatEvent
            {
                Id = e.GlobalChatEventId,
                Title = e.Title,
                Description = e.Description,
                StartsUtc = e.StartsUtc,
                Duration = e.Duration,
                IsOpen = e.IsOpen,
                Status = e.Status,
                CreatedUtc = e.CreatedUtc,
                StartedUtc = e.StartedUtc,
                EndedUtc = e.EndedUtc,
                CreatedBy = GeneralUserProjections.Projection.Splice(e.CreatedBy),
                Participants = e.Participants
                    .Select(p => ParticipantProjection.Splice(p))
                    .ToList()
            }));

    /// <summary>
    /// EF projection to the domain event participant
    /// </summary>
    public static IQueryable<GlobalChatEventParticipant> ProjectToGlobalChatEventParticipant(
        this IQueryable<DbGlobalChatEventParticipant> query) =>
        query.Select(ExpressionSplicer.Expand(ParticipantProjection));
}
