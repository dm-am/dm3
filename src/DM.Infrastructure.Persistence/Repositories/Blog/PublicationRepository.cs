using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;

using DM.Infrastructure.Persistence.Shared.Likes;
using DM.Infrastructure.Persistence.Shared.Users;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IPublicationRepository" />
internal class PublicationRepository : IPublicationRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PublicationRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public async Task<int> CountPublications(Guid blogId, Guid? rubricId, Guid viewerId, bool everyUnpublished,
        CancellationToken ct = default)
    {
        var query = _dbContext.Publications
            .TagWith("DM.Blog.CountPublications")
            .Where(p => !p.IsRemoved && p.BlogId == blogId);

        if (!everyUnpublished)
        {
            query = query.Where(p => p.IsPublished || p.AuthorId == viewerId);
        }

        if (rubricId.HasValue)
        {
            query = query.Where(p => p.RubricId == rubricId);
        }

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Publication>> GetPublications(
        Guid blogId, Guid? rubricId, Guid viewerId, bool everyUnpublished, PagingData paging, CancellationToken ct = default)
    {
        // No Include: the query ends in ProjectTo, which builds its own Select and makes
        // EF drop every Include with a warning. Author and rubric come from the mapping
        // expression.
        var query = _dbContext.Publications
            .TagWith("DM.Blog.ListPublications")
            .Where(p => !p.IsRemoved && p.BlogId == blogId);

        if (!everyUnpublished)
        {
            query = query.Where(p => p.IsPublished || p.AuthorId == viewerId);
        }

        if (rubricId.HasValue)
        {
            query = query.Where(p => p.RubricId == rubricId);
        }

        var publications = await query
            .OrderByDescending(p => p.PublishedUtc ?? p.CreatedUtc)
            .Page(paging)
            .ProjectToPublication()
            .ToListAsync(ct);

        await FillLikes(publications, ct);
        return publications;
    }

    /// <inheritdoc />
    public async Task<Publication?> GetPublication(Guid publicationId, CancellationToken ct = default)
    {
        var publication = await _dbContext.Publications
            .TagWith("DM.Blog.GetPublication")
            .Where(p => p.PublicationId == publicationId)
            .ProjectToPublication()
            .FirstOrDefaultAsync(ct);

        await FillLikes(publication, ct);
        return publication;
    }

    /// <inheritdoc />
    public async Task<Publication?> GetBestUserPublication(IReadOnlyCollection<Guid> authorIds, CancellationToken ct = default)
    {
        // Single-query "best" lookup: sort by the same likes subquery
        // pattern used by topics/comments, take the top row, project to
        // the API DTO. Soft-deleted + unpublished entries are filtered
        // out so the profile widget can never surface drafts.
        var publication = await _dbContext.Publications
            .TagWith("DM.Blog.GetBestUserPublication")
            .Where(p => !p.IsRemoved && p.IsPublished && authorIds.Contains(p.AuthorId))
            .OrderByDescending(p => _dbContext.Likes.Count(l =>
                !l.IsRemoved &&
                l.EntityId == p.PublicationId &&
                l.EntityType == Domain.Core.Enums.LikeEntityType.Publication))
            // Tie-breaker: newer-first so two zero-like publications still
            // produce a deterministic result rather than relying on the
            // server's insertion order.
            .ThenByDescending(p => p.PublishedUtc ?? p.CreatedUtc)
            .ProjectToPublication()
            .FirstOrDefaultAsync(ct);

        await FillLikes(publication, ct);
        return publication;
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<Publication> CreatePublication(CreatePublicationEntity entity, CancellationToken ct = default)
    {
        var publication = new DbPublication
        {
            PublicationId = entity.PublicationId,
            BlogId = entity.BlogId,
            RubricId = entity.RubricId,
            AuthorId = entity.AuthorId,
            Title = entity.Title,
            Content = entity.Content,
            Preview = entity.Preview ?? "",
            IsPublished = entity.PublishImmediately,
            PublishedUtc = entity.PublishImmediately ? entity.CreatedUtc : null,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false
        };

        // Both writes or neither, and through the strategy because the API host
        // configures EnableRetryOnFailure.
        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            // The count is raised in the database rather than on a blog row read into
            // the change tracker: two publications written at once both read the same
            // number and both wrote it back plus one, so the blog counted one of the
            // two and nothing recomputes the column afterwards. Zero rows means the
            // blog is gone or removed, which the tracked read used to answer by
            // throwing - the insert below would fail on the foreign key anyway, but
            // the meaning belongs where the row is missing.
            var counted = await _dbContext.Blogs
                .Where(b => b.BlogId == entity.BlogId)
                .ExecuteUpdateAsync(
                    b => b.SetProperty(x => x.PublicationCount, x => x.PublicationCount + 1),
                    cancellation);
            if (counted == 0)
            {
                throw new InvalidOperationException($"Blog {entity.BlogId} not found");
            }

            // The update above holds the blog row until commit, which serializes
            // creation per blog: that is what makes MAX + 1 an allocation rather
            // than a guess, exactly as the board row does for topic numbers.
            publication.PublicationNumber = await NextPublicationNumber(entity.BlogId, cancellation);
            _dbContext.Publications.Add(publication);
            await _dbContext.SaveChangesAsync(cancellation);

            await transaction.CommitAsync(cancellation);
        }, ct);

        return await GetPublication(entity.PublicationId, ct) ?? throw new InvalidOperationException("Publication not found after creation");
    }

    /// <inheritdoc />
    public async Task<Publication> UpdatePublication(UpdatePublicationEntity entity, CancellationToken ct = default)
    {
        var publication = await _dbContext.Publications.FindAsync([entity.PublicationId], ct);
        if (publication == null)
        {
            throw new InvalidOperationException($"Publication {entity.PublicationId} not found");
        }

        if (entity.ClearRubric)
            publication.RubricId = null;
        else if (entity.RubricId.HasValue)
            publication.RubricId = entity.RubricId.Value;

        if (!string.IsNullOrWhiteSpace(entity.Title))
            publication.Title = entity.Title;
        if (!string.IsNullOrWhiteSpace(entity.Content))
            publication.Content = entity.Content;
        if (!string.IsNullOrWhiteSpace(entity.Preview))
            publication.Preview = entity.Preview;

        if (entity.IsPublished.HasValue)
        {
            var wasPublished = publication.IsPublished;
            publication.IsPublished = entity.IsPublished.Value;
            if (!wasPublished && publication.IsPublished)
            {
                publication.PublishedUtc = entity.UpdatedUtc;
            }
        }

        publication.ModifiedUtc = entity.UpdatedUtc;
        publication.ModifiedByUserId = entity.ModifiedByUserId;
        await _dbContext.SaveChangesAsync(ct);

        return await GetPublication(entity.PublicationId, ct) ?? throw new InvalidOperationException("Publication not found after update");
    }

    /// <inheritdoc />
    public async Task DeletePublication(Guid publicationId, Guid deletedByUserId, CancellationToken ct = default)
    {
        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            // One statement reads and writes the row: a second removal of the same
            // publication arriving together with this one waits on the row lock and
            // then re-tests the predicate against the committed state, so exactly one
            // of the two matches and the blog's count comes down once. Read, check and
            // mark over three steps used to take it down twice for one publication,
            // with nothing that recomputes the column and no row left to say what the
            // right number was.
            var now = _dateTimeProvider.Now;
            var marked = await _dbContext.Publications
                .Where(p => p.PublicationId == publicationId && !p.IsRemoved)
                .ExecuteUpdateAsync(p => p
                    .SetProperty(x => x.IsRemoved, true)
                    .SetProperty(x => x.DeletedByUserId, (Guid?)deletedByUserId)
                    .SetProperty(x => x.DeletedUtc, (DateTimeOffset?)now), cancellation);

            if (marked == 1)
            {
                // After the mark, so the row it reads is the one this transaction now
                // holds; IgnoreQueryFilters, because that row is removed and the global
                // filter hides it.
                var blogId = await _dbContext.Publications
                    .IgnoreQueryFilters()
                    .TagWith("DM.Blog.RemovedPublicationBlog")
                    .Where(p => p.PublicationId == publicationId)
                    .Select(p => p.BlogId)
                    .FirstAsync(cancellation);

                // Floored at zero rather than trusted: the column is stored and nothing
                // recomputes it, so a count that has already drifted below the blog
                // must not go negative and stay there.
                await _dbContext.Blogs
                    .Where(b => b.BlogId == blogId)
                    .ExecuteUpdateAsync(b => b.SetProperty(
                        x => x.PublicationCount, x => x.PublicationCount > 0 ? x.PublicationCount - 1 : 0),
                        cancellation);
            }

            await transaction.CommitAsync(cancellation);
        }, ct);
    }

    // ═══ HELPERS ═══

    /// <summary>
    /// The number the next publication of this blog takes.
    /// </summary>
    /// <remarks>
    /// Callers hold the blog row, which is what makes MAX + 1 an allocation: two
    /// of these running at once without it read the same maximum. Every
    /// publication written by the site used to be stored as 0, and only the seed
    /// numbered its rows.
    ///
    /// IgnoreQueryFilters: a removed publication keeps its number, so the next one
    /// does not take the number of the newest removed one.
    /// </remarks>
    private async Task<int> NextPublicationNumber(Guid blogId, CancellationToken ct)
    {
        var highest = await _dbContext.Publications
            .IgnoreQueryFilters()
            .TagWith("DM.Blog.MaxPublicationNumber")
            .Where(p => p.BlogId == blogId)
            .Select(p => (int?)p.PublicationNumber)
            .MaxAsync(ct);

        return (highest ?? 0) + 1;
    }

    /// <summary>
    /// Single-publication overload of the backfill below.
    /// </summary>
    private Task FillLikes(Publication? publication, CancellationToken ct) =>
        publication is null
            ? Task.CompletedTask
            : FillLikes(new[] { publication }, ct);

    /// <summary>
    /// Backfill <see cref="Publication.Likes"/> for a page of publications.
    /// </summary>
    private Task FillLikes(IReadOnlyCollection<Publication> publications, CancellationToken ct) =>
        LikeBackfill.Fill(_dbContext, publications, Domain.Core.Enums.LikeEntityType.Publication,
            p => p.Id, (p, likes) => p.Likes = likes,
            "DM.Blog.PublicationLikes", "DM.Blog.PublicationLikers", ct);
}
