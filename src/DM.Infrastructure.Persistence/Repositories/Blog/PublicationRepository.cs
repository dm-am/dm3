using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IPublicationRepository" />
internal class PublicationRepository : IPublicationRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PublicationRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _dateTimeProvider = dateTimeProvider;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public async Task<int> CountPublications(Guid blogId, Guid? rubricId, bool includeUnpublished, CancellationToken ct = default)
    {
        var query = _dbContext.Publications
            .TagWith("DM.Blog.CountPublications")
            .Where(p => !p.IsRemoved && p.BlogId == blogId);

        if (!includeUnpublished)
        {
            query = query.Where(p => p.IsPublished);
        }

        if (rubricId.HasValue)
        {
            query = query.Where(p => p.RubricId == rubricId);
        }

        return await query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Publication>> GetPublications(
        Guid blogId, Guid? rubricId, bool includeUnpublished, PagingData paging, CancellationToken ct = default)
    {
        // No Include: the query ends in ProjectTo, which builds its own Select and makes
        // EF drop every Include with a warning. Author and rubric come from the mapping
        // expression.
        var query = _dbContext.Publications
            .TagWith("DM.Blog.ListPublications")
            .Where(p => !p.IsRemoved && p.BlogId == blogId);

        if (!includeUnpublished)
        {
            query = query.Where(p => p.IsPublished);
        }

        if (rubricId.HasValue)
        {
            query = query.Where(p => p.RubricId == rubricId);
        }

        var publications = await query
            .OrderByDescending(p => p.PublishedUtc ?? p.CreatedUtc)
            .Page(paging)
            .ProjectTo<Publication>(_mapper.ConfigurationProvider)
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
            .ProjectTo<Publication>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        await FillLikes(publication, ct);
        return publication;
    }

    /// <inheritdoc />
    public async Task<Publication?> GetBestUserPublication(Guid authorId, CancellationToken ct = default)
    {
        // Single-query "best" lookup: sort by the same likes subquery
        // pattern used by topics/comments, take the top row, project to
        // the API DTO. Soft-deleted + unpublished entries are filtered
        // out so the profile widget can never surface drafts.
        var publication = await _dbContext.Publications
            .TagWith("DM.Blog.GetBestUserPublication")
            .Where(p => !p.IsRemoved && p.IsPublished && p.AuthorId == authorId)
            .OrderByDescending(p => _dbContext.Likes.Count(l =>
                !l.IsRemoved &&
                l.EntityId == p.PublicationId &&
                l.EntityType == Domain.Core.Enums.LikeEntityType.Publication))
            // Tie-breaker: newer-first so two zero-like publications still
            // produce a deterministic result rather than relying on the
            // server's insertion order.
            .ThenByDescending(p => p.PublishedUtc ?? p.CreatedUtc)
            .ProjectTo<Publication>(_mapper.ConfigurationProvider)
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
            CommentsEnabled = entity.CommentsEnabled,
            IsPublished = entity.PublishImmediately,
            PublishedUtc = entity.PublishImmediately ? entity.CreatedUtc : null,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false
        };

        _dbContext.Publications.Add(publication);

        // Update blog publication count
        var blog = await _dbContext.Blogs.FirstAsync(b => b.BlogId == entity.BlogId, ct);
        blog.PublicationCount++;

        await _dbContext.SaveChangesAsync(ct);

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
        if (entity.CommentsEnabled.HasValue)
            publication.CommentsEnabled = entity.CommentsEnabled.Value;

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
        var publication = await _dbContext.Publications
            .Include(p => p.Blog)
            .FirstOrDefaultAsync(p => p.PublicationId == publicationId, ct);

        if (publication != null)
        {
            SoftDelete.Mark(publication, deletedByUserId, _dateTimeProvider.Now);

            // Update blog publication count
            publication.Blog.PublicationCount--;

            await _dbContext.SaveChangesAsync(ct);
        }
    }

    // ═══ HELPERS ═══

    /// <summary>
    /// Single-publication overload of the backfill below.
    /// </summary>
    private Task FillLikes(Publication? publication, CancellationToken ct) =>
        publication is null
            ? Task.CompletedTask
            : FillLikes(new[] { publication }, ct);

    /// <summary>
    /// Backfill <see cref="Publication.Likes"/> for a page of publications.
    ///
    /// The mapping profile ignores Likes — they live in the polymorphic Likes
    /// table (EntityType + EntityId) with no navigation to project through —
    /// and nothing filled them afterwards, so every read answered with an
    /// empty list. That cost more than a zero on a card: the like/unlike path
    /// asks the very same list whether the viewer has already liked, so a
    /// repeat like was accepted and an unlike was always refused.
    ///
    /// Two batched queries for the whole page instead of a correlated
    /// subquery per row (PERFORMANCE.md → "Avoid inline aggregations"): the
    /// (publication, liker) pairs first, then one projection of the distinct
    /// likers. publicationIds is a List&lt;Guid&gt;, NOT Guid[] — EF Core's
    /// translator has a Guid[] edge case that throws TypeLoadException on the
    /// ReadOnlySpan&lt;Guid&gt; interpreter path (see TopicRepository).
    /// </summary>
    private async Task FillLikes(IReadOnlyCollection<Publication> publications, CancellationToken ct)
    {
        if (publications.Count == 0)
        {
            return;
        }

        var publicationIds = publications.Select(p => p.Id).ToList();
        var pairs = await _dbContext.Likes
            .TagWith("DM.Blog.PublicationLikes")
            .AsNoTracking()
            .Where(l =>
                !l.IsRemoved &&
                l.EntityType == Domain.Core.Enums.LikeEntityType.Publication &&
                publicationIds.Contains(l.EntityId))
            .Select(l => new { l.EntityId, l.UserId })
            .ToListAsync(ct);

        if (pairs.Count == 0)
        {
            return;
        }

        var likerIds = pairs.Select(p => p.UserId).Distinct().ToList();
        var likers = await _dbContext.Users
            .TagWith("DM.Blog.PublicationLikers")
            .AsNoTracking()
            .Where(u => likerIds.Contains(u.UserId))
            .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
            .ToDictionaryAsync(u => u.UserId, ct);

        // A liker filtered out by the soft-delete filter has no projection;
        // their like is dropped rather than crashing the page.
        var byPublication = pairs
            .Where(p => likers.ContainsKey(p.UserId))
            .GroupBy(p => p.EntityId)
            .ToDictionary(g => g.Key, g => g.Select(p => likers[p.UserId]).ToArray());

        foreach (var publication in publications)
        {
            if (byPublication.TryGetValue(publication.Id, out var likes))
            {
                publication.Likes = likes;
            }
        }
    }
}
