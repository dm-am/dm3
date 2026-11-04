using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.InitialData;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// The forum module of the model: topic numbers, the comments of a topic, and the
/// boards and topics the site ships with.
/// </summary>
internal static class ForumModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        AuditRelationships.DeletedBy<Topic>(modelBuilder);

        // TopicNumber is the topic URL key, allocated as MAX+1, so the constraint stops
        // two concurrent creates sharing a number. Removed topics keep their number.
        modelBuilder.Entity<Topic>()
            .HasIndex(t => new { t.BoardId, t.TopicNumber })
            .IsUnique();

        // Comment.EntityId is polymorphic (Topic, Game, Blog or Publication): the
        // FK_Comments_Topics_EntityId EF generates from this pair is removed from the
        // migration by hand, see the comment in InitialCreate.cs.
        modelBuilder.Entity<Topic>()
            .HasMany(t => t.Comments)
            .WithOne(c => c.Topic)
            .HasForeignKey(c => c.EntityId)
            .OnDelete(DeleteBehavior.ClientCascade);

        modelBuilder.Entity<Board>().HasData(BoardCatalog.Boards());

        modelBuilder.Entity<Topic>().HasData(BoardCatalog.SystemTopics());
    }
}
