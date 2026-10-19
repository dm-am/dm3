using DM.Infrastructure.Persistence.Entities.Blog;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// The blog module of the model: assistants, the readable key of a blog, the
/// numbers of its publications and its blacklist.
/// </summary>
internal static class BlogModel
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        // A person assists a blog once, for the reason written beside the game's
        // assistants in GameModel.
        modelBuilder.Entity<BlogAssistant>()
            .HasIndex(a => new { a.BlogId, a.UserId })
            .IsUnique();

        // A blog resolves its page by a readable key.
        modelBuilder.Entity<Blog>()
            .HasIndex(b => b.PublicId)
            .IsUnique();

        // PublicationNumber is allocated as MAX+1 like TopicNumber (ForumModel).
        // Removed publications keep their number.
        modelBuilder.Entity<Publication>()
            .HasIndex(p => new { p.BlogId, p.PublicationNumber })
            .IsUnique();

        // Stops two concurrent adds of one name, as for the game's blacklist (GameModel).
        // Leading with BlogId, so it replaces the conventional FK index.
        modelBuilder.Entity<BlogBlacklist>()
            .HasIndex(b => new { b.BlogId, b.BlockedUserId }).IsUnique();
    }
}
