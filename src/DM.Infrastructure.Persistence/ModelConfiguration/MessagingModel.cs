using DM.Domain.Core.Chats;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Messaging;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// The messaging module of the model: messages, chats and the global chat, and the
/// events held in it.
/// </summary>
internal static class MessagingModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        AuditRelationships.DeletedBy<Message>(modelBuilder);

        // Composite index backing message cursor/keyset pagination and search
        // (filter by ChatId, order by CreatedUtc, tie-break by MessageId).
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ChatId, m.CreatedUtc, m.MessageId });

        // The readable chat id is resolved by equality in GET /chats/{id}. Without
        // uniqueness a collision between an encoded serial and the reserved name of
        // the global chat would not fail - it would silently return whichever row
        // the plan happened to reach first.
        modelBuilder.Entity<Chat>()
            .HasIndex(c => c.PublicId)
            .IsUnique();

        modelBuilder.Entity<Chat>().HasData(
            new Chat
            {
                ChatId = WellKnownChats.GlobalChatId,
                Type = ChatType.Global,
                Title = "Глобальный чат",
                // Every other chat gets its readable id encoded from the serial the
                // database assigns on insert; a seeded row never goes through that
                // path, so the one chat that is addressable by name gets it here.
                PublicId = "global"
            });

        // One person is on one event's list once: joining is check-then-insert, so
        // only the constraint stops two concurrent joins. Leads with GlobalChatEventId,
        // so it replaces the plain FK index.
        modelBuilder.Entity<GlobalChatEventParticipant>()
            .HasIndex(p => new { p.GlobalChatEventId, p.UserId })
            .IsUnique();

        // At most one event is live at a time, even when two organisers start events
        // at once. The value is the one Live is stored as.
        modelBuilder.Entity<GlobalChatEvent>()
            .HasIndex(e => e.Status)
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)GlobalChatEventStatus.Live}");
    }
}
