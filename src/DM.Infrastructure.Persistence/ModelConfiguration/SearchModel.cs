using DM.Domain.Core.Content;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Repositories.Search;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// What the full-text search reads: the visible text kept beside every indexed
/// body, the vectors generated from it with their indexes, and the extension the
/// substring searches use.
/// </summary>
/// <remarks>
/// The four bodies in one file, because their four vectors are built by one
/// spelling of the expression.
/// </remarks>
internal static class SearchModel
{
    internal static void Configure(ModelBuilder modelBuilder, bool isPostgres)
    {
        if (isPostgres)
        {
            // similarity() of the catalog and user searches needs it; declared on the model so a
            // regenerated migration keeps it.
            modelBuilder.HasPostgresExtension("pg_trgm");
        }

        // The visible text of every indexed body, kept beside it. Declared for all
        // providers and not only for Postgres: the in-memory store used by the unit
        // tier has to carry the same shape, and a column that exists in one and not
        // the other is a difference the tests cannot see.
        modelBuilder.Entity<Message>().Property<string>("SearchText").HasDefaultValue(string.Empty);
        modelBuilder.Entity<Post>().Property<string>("SearchText").HasDefaultValue(string.Empty);
        modelBuilder.Entity<Topic>().Property<string>("SearchText").HasDefaultValue(string.Empty);
        modelBuilder.Entity<Comment>().Property<string>("SearchText").HasDefaultValue(string.Empty);

        if (isPostgres)
        {
            Vectors(modelBuilder);
        }
    }

    // One spelling of the expression for all four columns; the dictionary is
    // SearchTextConfiguration, shared with the repositories that query them.
    private static string SearchVectorSql(string column) =>
        $"to_tsvector('{SearchTextConfiguration.Name}', regexp_replace(coalesce(\"{column}\", ''), " +
        $"'{PrivateBlockMarkup.BlockPattern}', ' ', '{PrivateBlockMarkup.BlockPatternFlags}'))";

    // Generated STORED tsvector columns with GIN indexes over SearchText, the visible
    // text SearchTextProjection writes; the regexp strips hidden blocks a second time
    // in case the projection did not.
    private static void Vectors(ModelBuilder modelBuilder)
    {
        TextVector<Message>(modelBuilder);

        // Hidden blocks are cut even where room settings open them: a generated
        // column cannot see the room, and the index stays narrower than the page.
        TextVector<Post>(modelBuilder);

        // A title match weighs more than a body match, and setweight lets ts_rank say so.
        modelBuilder.Entity<Topic>(b =>
        {
            b.Property<NpgsqlTsVector>("SearchVector")
                .HasComputedColumnSql(
                    $"setweight(to_tsvector('{SearchTextConfiguration.Name}', coalesce(\"Title\", '')), 'A') || " +
                    $"setweight({SearchVectorSql("SearchText")}, 'B')",
                    stored: true);
            b.HasIndex("SearchVector").HasMethod("gin").HasDatabaseName("IX_Topics_SearchVector");
        });

        // Comment.EntityId is polymorphic, so the column indexes every kind of comment;
        // the join of the query narrows a search to one kind.
        TextVector<Comment>(modelBuilder);
    }

    private static void TextVector<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.Property<NpgsqlTsVector>("SearchVector")
            .HasComputedColumnSql(SearchVectorSql("SearchText"), stored: true);
        entity.HasIndex("SearchVector").HasMethod("gin");
    }
}
