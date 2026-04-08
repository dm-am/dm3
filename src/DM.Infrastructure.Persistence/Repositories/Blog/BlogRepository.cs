using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBlogAssistant = DM.Infrastructure.Persistence.Entities.Blog.BlogAssistant;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbRubric = DM.Infrastructure.Persistence.Entities.Blog.Rubric;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IBlogRepository" />
internal class BlogRepository : IBlogRepository
{
    private static readonly TimeSpan ActivePeriod = TimeSpan.FromDays(30);

    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPublicIdService _publicIdService;

    /// <inheritdoc />
    public BlogRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IDateTimeProvider dateTimeProvider,
        IPublicIdService publicIdService)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _dateTimeProvider = dateTimeProvider;
        _publicIdService = publicIdService;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountPublicBlogs(
        string? search = null,
        ModuleStatus? status = null,
        IReadOnlyCollection<Guid>? hostUserIds = null,
        DateTimeOffset? createdFromUtc = null,
        DateTimeOffset? createdToUtc = null,
        DateTimeOffset? activatedFromUtc = null,
        DateTimeOffset? activatedToUtc = null,
        DateTimeOffset? closedFromUtc = null,
        DateTimeOffset? closedToUtc = null,
        IReadOnlyCollection<Guid>? excludeOwnerIds = null,
        CancellationToken ct = default)
    {
        var query = GetFilteredQuery(search, status, hostUserIds, createdFromUtc, createdToUtc,
            activatedFromUtc, activatedToUtc, closedFromUtc, closedToUtc, excludeOwnerIds);
        return query.CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetPublicBlogs(
        PagingData paging,
        string? search = null,
        ModuleStatus? status = null,
        IReadOnlyCollection<Guid>? hostUserIds = null,
        string? sortBy = null,
        string? sortOrder = null,
        DateTimeOffset? createdFromUtc = null,
        DateTimeOffset? createdToUtc = null,
        DateTimeOffset? activatedFromUtc = null,
        DateTimeOffset? activatedToUtc = null,
        DateTimeOffset? closedFromUtc = null,
        DateTimeOffset? closedToUtc = null,
        IReadOnlyCollection<Guid>? excludeOwnerIds = null,
        CancellationToken ct = default)
    {
        var query = GetFilteredQuery(search, status, hostUserIds, createdFromUtc, createdToUtc,
            activatedFromUtc, activatedToUtc, closedFromUtc, closedToUtc, excludeOwnerIds);

        // Apply ordering
        var orderedQuery = ApplySorting(query, search, sortBy, sortOrder);

        var blogs = await orderedQuery
            .Page(paging)
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        await FillBlogSubscriberIds(blogs, ct);
        return blogs;
    }

    private IQueryable<DbBlog> GetFilteredQuery(
        string? search,
        ModuleStatus? status,
        IReadOnlyCollection<Guid>? hostUserIds,
        DateTimeOffset? createdFromUtc,
        DateTimeOffset? createdToUtc,
        DateTimeOffset? activatedFromUtc,
        DateTimeOffset? activatedToUtc,
        DateTimeOffset? closedFromUtc,
        DateTimeOffset? closedToUtc,
        IReadOnlyCollection<Guid>? excludeOwnerIds)
    {
        // Show Active, Closed, and Draft blogs with public visibility (like games)
        var query = _dbContext.Blogs
            .TagWith("DM.Blog.ListPublic")
            .Where(b => !b.IsRemoved &&
                (b.Status != ModuleStatus.Draft || b.DraftVisibility == DraftVisibility.Public))
            .Where(b => excludeOwnerIds == null || !excludeOwnerIds.Contains(b.AuthorId));

        // Status filter
        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        // Host filter (owner OR assistant, OR logic)
        if (hostUserIds?.Count > 0)
        {
            var assistantBlogIds = _dbContext.BlogAssistants
                .Where(a => hostUserIds.Contains(a.UserId))
                .Select(a => a.BlogId);

            query = query.Where(b => hostUserIds.Contains(b.AuthorId) || assistantBlogIds.Contains(b.BlogId));
        }

        // Text search with fuzzy matching
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = "%" + search.Replace("%", "\\%").Replace("_", "\\_") + "%";
            var searchLower = search.ToLower();
            query = query.Where(b =>
                EF.Functions.ILike(b.Title, searchPattern) ||
                EF.Functions.TrigramsSimilarity(b.Title, searchLower) > 0.3);
        }

        // Created date range
        if (createdFromUtc.HasValue)
        {
            query = query.Where(b => b.CreatedUtc >= createdFromUtc.Value);
        }
        if (createdToUtc.HasValue)
        {
            query = query.Where(b => b.CreatedUtc <= createdToUtc.Value);
        }

        // Activated date range (excludes blogs without ActivatedUtc)
        if (activatedFromUtc.HasValue || activatedToUtc.HasValue)
        {
            query = query.Where(b => b.ActivatedUtc.HasValue);
            if (activatedFromUtc.HasValue)
            {
                query = query.Where(b => b.ActivatedUtc >= activatedFromUtc.Value);
            }
            if (activatedToUtc.HasValue)
            {
                query = query.Where(b => b.ActivatedUtc <= activatedToUtc.Value);
            }
        }

        // Closed date range (excludes blogs without ClosedUtc)
        if (closedFromUtc.HasValue || closedToUtc.HasValue)
        {
            query = query.Where(b => b.ClosedUtc.HasValue);
            if (closedFromUtc.HasValue)
            {
                query = query.Where(b => b.ClosedUtc >= closedFromUtc.Value);
            }
            if (closedToUtc.HasValue)
            {
                query = query.Where(b => b.ClosedUtc <= closedToUtc.Value);
            }
        }

        return query;
    }

    private IOrderedQueryable<DbBlog> ApplySorting(IQueryable<DbBlog> query, string? search, string? sortBy, string? sortOrder)
    {
        var isAscending = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        // Search without explicit sortBy = relevance ranking (best UX)
        if (!string.IsNullOrWhiteSpace(search) && string.IsNullOrEmpty(sortBy))
        {
            var searchLower = search.ToLower();
            return query
                .OrderByDescending(b => b.Title.ToLower() == searchLower) // Exact match first
                .ThenByDescending(b => EF.Functions.ILike(b.Title, search + "%")) // Prefix match
                .ThenByDescending(b => EF.Functions.TrigramsSimilarity(b.Title, searchLower)) // Fuzzy score
                .ThenBy(b => b.Title);
        }

        // Explicit sort selected or no search - use specified sort
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => isAscending
                ? query.OrderBy(b => b.Title)
                : query.OrderByDescending(b => b.Title),

            "status" => isAscending
                ? query.OrderBy(b => b.Status)
                    .ThenBy(b => b.Status == ModuleStatus.Draft ? b.CreatedUtc :
                                 b.Status == ModuleStatus.Active ? b.ActivatedUtc ?? b.CreatedUtc :
                                 b.ClosedUtc ?? b.CreatedUtc)
                : query.OrderByDescending(b => b.Status)
                    .ThenByDescending(b => b.Status == ModuleStatus.Draft ? b.CreatedUtc :
                                           b.Status == ModuleStatus.Active ? b.ActivatedUtc ?? b.CreatedUtc :
                                           b.ClosedUtc ?? b.CreatedUtc),

            "popularity" => isAscending
                ? query.OrderBy(b => b.PopularityScore).ThenBy(b => b.Title)
                : query.OrderByDescending(b => b.PopularityScore).ThenBy(b => b.Title),

            "activated" => isAscending
                ? query.OrderBy(b => b.ActivatedUtc.HasValue).ThenBy(b => b.ActivatedUtc ?? DateTimeOffset.MaxValue)
                : query.OrderByDescending(b => b.ActivatedUtc.HasValue).ThenByDescending(b => b.ActivatedUtc ?? DateTimeOffset.MinValue),

            "closed" => isAscending
                ? query.OrderBy(b => b.ClosedUtc.HasValue).ThenBy(b => b.ClosedUtc ?? DateTimeOffset.MaxValue)
                : query.OrderByDescending(b => b.ClosedUtc.HasValue).ThenByDescending(b => b.ClosedUtc ?? DateTimeOffset.MinValue),

            // default: created
            _ => isAscending
                ? query.OrderBy(b => b.CreatedUtc)
                : query.OrderByDescending(b => b.CreatedUtc)
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetUserBlogs(Guid userId, CancellationToken ct = default)
    {
        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.ListByUser")
            .Where(b => !b.IsRemoved && b.AuthorId == userId)
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        await FillBlogSubscriberIds(blogs, ct);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<BlogDto?> Get(Guid blogId, CancellationToken ct = default)
    {
        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        return await _dbContext.Blogs
            .TagWith("DM.Blog.Get")
            .Where(b => b.BlogId == blogId)
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlogDto?> GetByPublicId(string publicId, CancellationToken ct = default)
    {
        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        return await _dbContext.Blogs
            .TagWith("DM.Blog.GetByPublicId")
            .Where(b => b.PublicId == publicId)
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlogDto?> GetByOwnerUsernameAsync(string username, CancellationToken ct = default)
    {
        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        return await _dbContext.Blogs
            .TagWith("DM.Blog.GetByUsername")
            .Where(b => b.Author.Username.ToLower() == username.ToLower())
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

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
        var query = _dbContext.Publications
            .TagWith("DM.Blog.ListPublications")
            .Include(p => p.Author)
            .Include(p => p.Rubric)
            .Where(p => !p.IsRemoved && p.BlogId == blogId);

        if (!includeUnpublished)
        {
            query = query.Where(p => p.IsPublished);
        }

        if (rubricId.HasValue)
        {
            query = query.Where(p => p.RubricId == rubricId);
        }

        return await query
            .OrderByDescending(p => p.PublishedUtc ?? p.CreatedUtc)
            .Page(paging)
            .ProjectTo<Publication>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Publication?> GetPublication(Guid publicationId, CancellationToken ct = default)
    {
        return await _dbContext.Publications
            .TagWith("DM.Blog.GetPublication")
            .Include(p => p.Blog)
            .ThenInclude(b => b.Author)
            .Include(p => p.Author)
            .Include(p => p.Rubric)
            .Where(p => p.PublicationId == publicationId)
            .ProjectTo<Publication>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Rubrics
            .TagWith("DM.Blog.ListRubrics")
            .Where(r => !r.IsRemoved && r.BlogId == blogId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .ProjectTo<Rubric>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(Rubric? rubric, Guid blogId)> GetRubric(Guid rubricId, CancellationToken ct = default)
    {
        var dbRubric = await _dbContext.Rubrics
            .TagWith("DM.Blog.GetRubric")
            .Where(r => r.RubricId == rubricId && !r.IsRemoved)
            .FirstOrDefaultAsync(ct);

        if (dbRubric == null)
        {
            return (null, Guid.Empty);
        }

        var rubric = _mapper.Map<Rubric>(dbRubric);
        return (rubric, dbRubric.BlogId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetPopularBlogs(
        int count, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        // Use pre-computed PopularityScore for efficient sorting (updated by PopularityScoreService)
        // Show Active, Closed, and Draft blogs with public visibility (like games)
        // Status grouping: Active first (desc true=first), then Closed, then Draft (false sorts last)
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.Popular")
            .Where(b => !b.IsRemoved &&
                (b.Status != ModuleStatus.Draft || b.DraftVisibility == DraftVisibility.Public))
            .Where(b => excludeOwnerIds == null || !excludeOwnerIds.Contains(b.AuthorId))
            .Where(b => b.PopularityScore > 0)
            .OrderByDescending(b => b.Status == ModuleStatus.Active)
            .ThenByDescending(b => b.Status == ModuleStatus.Closed)
            .ThenByDescending(b => b.PopularityScore)
            .ThenBy(b => b.Title)
            .Take(count)
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        await FillBlogSubscriberIds(blogs, ct);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetReaders(Guid blogId, CancellationToken ct = default)
    {
        // Readers are now stored in Subscriptions table
        var subscriberIds = await _dbContext.Subscriptions
            .TagWith("DM.Blog.ReaderIds")
            .Where(s => s.TargetType == SubscriptionTargetType.Blog && s.TargetId == blogId)
            .Select(s => s.SubscriberId)
            .ToListAsync(ct);

        return await _dbContext.Users
            .TagWith("DM.Blog.Readers")
            .Where(u => subscriberIds.Contains(u.UserId))
            .Select(u => new GeneralUser
            {
                UserId = u.UserId,
                Username = u.Username,
                Role = u.Role,
                Status = u.Status,
                LastActivityUtc = u.LastActivityUtc,
                SmallPictureUrl = u.AvatarUpload != null ? (u.AvatarUpload.SmallFilePath ?? u.AvatarUpload.FilePath) : null,
                MediumPictureUrl = u.AvatarUpload != null ? (u.AvatarUpload.MediumFilePath ?? u.AvatarUpload.FilePath) : null
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetAssistants(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.BlogAssistants
            .TagWith("DM.Blog.Assistants")
            .Include(a => a.User)
            .Where(a => a.BlogId == blogId)
            .Select(a => new GeneralUser
            {
                UserId = a.User.UserId,
                Username = a.User.Username,
                Role = a.User.Role,
                Status = a.User.Status,
                LastActivityUtc = a.User.LastActivityUtc,
                SmallPictureUrl = a.User.AvatarUpload != null ? (a.User.AvatarUpload.SmallFilePath ?? a.User.AvatarUpload.FilePath) : null,
                MediumPictureUrl = a.User.AvatarUpload != null ? (a.User.AvatarUpload.MediumFilePath ?? a.User.AvatarUpload.FilePath) : null
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetAssistantsWithJoinDate(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.BlogAssistants
            .TagWith("DM.Blog.AssistantsWithJoinDate")
            .Where(a => a.BlogId == blogId)
            .Select(a => new BlogUser
            {
                User = new GeneralUser
                {
                    UserId = a.User.UserId,
                    Username = a.User.Username,
                    Role = a.User.Role,
                    Status = a.User.Status,
                    LastActivityUtc = a.User.LastActivityUtc,
                    SmallPictureUrl = a.User.AvatarUpload != null ? (a.User.AvatarUpload.SmallFilePath ?? a.User.AvatarUpload.FilePath) : null,
                    MediumPictureUrl = a.User.AvatarUpload != null ? (a.User.AvatarUpload.MediumFilePath ?? a.User.AvatarUpload.FilePath) : null
                },
                Role = BlogRole.Assistant,
                JoinedUtc = a.JoinedUtc
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetByIds(IEnumerable<Guid> blogIds, CancellationToken ct = default)
    {
        var blogIdList = blogIds.ToList();
        if (blogIdList.Count == 0)
            return [];

        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.GetByIds")
            .Where(b => !b.IsRemoved && blogIdList.Contains(b.BlogId))
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        await FillBlogSubscriberIds(blogs, ct);
        return blogs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogDto>> GetOwnBlogs(Guid userId, CancellationToken ct = default)
    {
        // Get blogs where user is owner, mentor, or assistant
        var assistantBlogIds = await _dbContext.BlogAssistants
            .Where(a => a.UserId == userId)
            .Select(a => a.BlogId)
            .ToListAsync(ct);

        // Note: Include not needed with ProjectTo - AutoMapper generates SQL subqueries
        var blogs = await _dbContext.Blogs
            .TagWith("DM.Blog.GetOwnBlogs")
            .Where(b => !b.IsRemoved &&
                (b.AuthorId == userId || b.MentorId == userId || assistantBlogIds.Contains(b.BlogId)))
            .OrderByDescending(b => b.ActivatedUtc ?? b.CreatedUtc)
            .ProjectTo<BlogDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        await FillBlogSubscriberIds(blogs, ct);
        return blogs;
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<BlogDto> CreateBlog(CreateBlogEntity entity, CancellationToken ct = default)
    {
        var blog = new DbBlog
        {
            BlogId = entity.BlogId,
            AuthorId = entity.OwnerId,
            Title = entity.Title,
            Description = entity.Description ?? "",
            DraftVisibility = entity.DraftVisibility,
            CommentsEnabled = entity.CommentsEnabled,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false,
            // Temporary unique placeholder for PublicId (will be updated after SerialNumber is generated)
            PublicId = $"t{entity.BlogId:N}"[..10]
        };

        _dbContext.Blogs.Add(blog);
        await _dbContext.SaveChangesAsync(ct);

        // Generate PublicId from SerialNumber (which was auto-generated on insert)
        blog.PublicId = _publicIdService.Encode(blog.SerialNumber);
        await _dbContext.SaveChangesAsync(ct);

        return await Get(entity.BlogId, ct) ?? throw new InvalidOperationException("Blog not found after creation");
    }

    /// <inheritdoc />
    public async Task<BlogDto> UpdateBlog(UpdateBlogEntity entity, CancellationToken ct = default)
    {
        var blog = await _dbContext.Blogs.FindAsync([entity.BlogId], ct);
        if (blog == null)
        {
            throw new InvalidOperationException($"Blog {entity.BlogId} not found");
        }

        if (!string.IsNullOrWhiteSpace(entity.Title))
            blog.Title = entity.Title;
        if (!string.IsNullOrWhiteSpace(entity.Description))
            blog.Description = entity.Description;
        if (entity.DraftVisibility.HasValue)
            blog.DraftVisibility = entity.DraftVisibility.Value;
        if (entity.CommentsEnabled.HasValue)
            blog.CommentsEnabled = entity.CommentsEnabled.Value;

        blog.UpdatedUtc = entity.UpdatedUtc;
        await _dbContext.SaveChangesAsync(ct);

        return await Get(entity.BlogId, ct) ?? throw new InvalidOperationException("Blog not found after update");
    }

    /// <inheritdoc />
    public async Task DeleteBlog(Guid blogId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var blog = await _dbContext.Blogs.FirstOrDefaultAsync(b => b.BlogId == blogId, ct);
        if (blog != null)
        {
            blog.IsRemoved = true;
            blog.DeletedByUserId = deletedByUserId;
            blog.DeletedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<Rubric> CreateRubric(CreateRubricEntity entity, CancellationToken ct = default)
    {
        var rubric = new DbRubric
        {
            RubricId = entity.RubricId,
            BlogId = entity.BlogId,
            Title = entity.Title,
            SortOrder = entity.SortOrder,
            IsRemoved = false
        };

        _dbContext.Rubrics.Add(rubric);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Rubrics
            .TagWith("DM.Blog.CreatedRubric")
            .Where(r => r.RubricId == entity.RubricId)
            .ProjectTo<Rubric>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Rubric> UpdateRubric(UpdateRubricEntity entity, CancellationToken ct = default)
    {
        var rubric = await _dbContext.Rubrics.FindAsync([entity.RubricId], ct);
        if (rubric == null)
        {
            throw new InvalidOperationException($"Rubric {entity.RubricId} not found");
        }

        if (!string.IsNullOrWhiteSpace(entity.Title))
            rubric.Title = entity.Title;
        if (entity.SortOrder.HasValue)
            rubric.SortOrder = entity.SortOrder.Value;

        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Rubrics
            .TagWith("DM.Blog.UpdatedRubric")
            .Where(r => r.RubricId == entity.RubricId)
            .ProjectTo<Rubric>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteRubric(Guid rubricId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var rubric = await _dbContext.Rubrics.FirstOrDefaultAsync(r => r.RubricId == rubricId, ct);
        if (rubric != null)
        {
            rubric.IsRemoved = true;
            rubric.DeletedByUserId = deletedByUserId;
            rubric.DeletedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

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
            publication.IsRemoved = true;
            publication.DeletedByUserId = deletedByUserId;
            publication.DeletedUtc = DateTimeOffset.UtcNow;

            // Update blog publication count
            publication.Blog.PublicationCount--;

            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task AddAssistant(AddBlogAssistantEntity entity, CancellationToken ct = default)
    {
        var assistant = new DbBlogAssistant
        {
            BlogId = entity.BlogId,
            UserId = entity.UserId,
            JoinedUtc = entity.JoinedUtc
        };

        _dbContext.BlogAssistants.Add(assistant);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> IsAssistant(Guid blogId, Guid userId, CancellationToken ct = default) =>
        _dbContext.BlogAssistants.AnyAsync(
            a => a.BlogId == blogId && a.UserId == userId,
            ct);

    /// <inheritdoc />
    public async Task<bool> RemoveAssistant(Guid blogId, Guid userId, CancellationToken ct = default)
    {
        var assistant = await _dbContext.BlogAssistants
            .FirstOrDefaultAsync(a => a.BlogId == blogId && a.UserId == userId, ct);

        if (assistant == null)
        {
            return false;
        }

        _dbContext.BlogAssistants.Remove(assistant);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAssistantByUsername(Guid blogId, string username, CancellationToken ct = default)
    {
        var usernameLower = username.ToLower();
        var assistant = await _dbContext.BlogAssistants
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.BlogId == blogId && a.User.Username.ToLower() == usernameLower, ct);

        if (assistant == null)
        {
            return false;
        }

        _dbContext.BlogAssistants.Remove(assistant);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> IsSubscriber(Guid blogId, Guid userId, CancellationToken ct = default) =>
        _dbContext.Subscriptions.AnyAsync(
            s => s.TargetType == SubscriptionTargetType.Blog && s.TargetId == blogId && s.SubscriberId == userId,
            ct);

    // ═══ HELPERS ═══

    private async Task FillBlogSubscriberIds(IList<BlogDto> blogs, CancellationToken ct)
    {
        if (blogs.Count == 0) return;

        var blogIdSet = blogs.Select(b => b.Id).ToHashSet();
        var activeThreshold = _dateTimeProvider.Now - ActivePeriod;

        // Load subscriber IDs (for participation detection) - same pattern as GameRepository
        var subscriptionMap = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.Blog && blogIdSet.Contains(s.TargetId))
            .GroupBy(s => s.TargetId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(s => s.SubscriberId).ToHashSet(), ct);

        // Batch load subscriber usernames for tooltip (limit to first 20)
        var subscriberData = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.Blog && blogIdSet.Contains(s.TargetId))
            .Select(s => new { s.TargetId, Username = s.Subscriber.Username })
            .ToListAsync(ct);

        var subscriberUsernamesMap = subscriberData
            .GroupBy(s => s.TargetId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => s.Username).Take(20).ToList());

        // Load active subscribers count (subscribers active in last 30 days)
        var activeSubscribersData = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.Blog &&
                       blogIdSet.Contains(s.TargetId) &&
                       s.Subscriber.LastActivityUtc.HasValue &&
                       s.Subscriber.LastActivityUtc.Value > activeThreshold)
            .GroupBy(s => s.TargetId)
            .Select(g => new { BlogId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BlogId, x => x.Count, ct);

        foreach (var blog in blogs)
        {
            blog.SubscriberIds = subscriptionMap.GetValueOrDefault(blog.Id, []);
            blog.SubscriberUsernames = subscriberUsernamesMap.GetValueOrDefault(blog.Id, []);
            blog.ActiveSubscribersCount = activeSubscribersData.GetValueOrDefault(blog.Id, 0);
        }
    }
}
