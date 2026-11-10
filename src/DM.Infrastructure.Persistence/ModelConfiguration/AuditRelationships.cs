using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

internal static class AuditRelationships
{
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
