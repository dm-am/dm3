using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// The dice rolls of the model. A roll is attached to a post by its foreign key
/// and by nothing else.
/// </summary>
internal static class DiceRollModel
{
    internal static void Configure(ModelBuilder modelBuilder, bool isPostgres)
    {
        // DiceRolls: written only inside the post's transaction. The FK
        // index the convention adds serves the room render's batch read.
        modelBuilder.Entity<DiceRoll>(entity =>
        {
            entity.HasOne<Post>()
                .WithMany()
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(d => d.Result).IsJson(isPostgres);
        });
    }
}
