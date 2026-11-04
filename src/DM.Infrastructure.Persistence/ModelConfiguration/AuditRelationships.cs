using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// Who removed a row and who last edited it: the relationships to the user that
/// the soft-deletable and the editable entities of every module declare alike.
/// </summary>
internal static class AuditRelationships
{
    /// <summary>
    /// Configure DeletedBy relationship for ISoftDeletable entities without inverse properties
    /// </summary>
    internal static void DeletedBy<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .HasOne<User>("DeletedBy")
            .WithMany()
            .HasForeignKey("DeletedByUserId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// Configure DeletedBy and ModifiedBy relationships for editable entities without inverse properties
    /// </summary>
    internal static void Editable<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        DeletedBy<TEntity>(modelBuilder);
        modelBuilder.Entity<TEntity>()
            .HasOne<User>("ModifiedBy")
            .WithMany()
            .HasForeignKey("ModifiedByUserId")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
