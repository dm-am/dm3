using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Dto;
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
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BlogRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<int> CountPublicBlogs(IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        return _dbContext.Blogs
            .TagWith("DM.Blog.CountPublic")
            .Where(b => !b.IsRemoved && b.DraftVisibility == DraftVisibility.Public)
            .Where(b => excludeOwnerIds == null || !excludeOwnerIds.Contains(b.AuthorId))
            .CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogModel>> GetPublicBlogs(
        PagingData paging, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .TagWith("DM.Blog.ListPublic")
            .Include(b => b.Author)
            .Include(b => b.Publications.Where(p => !p.IsRemoved && p.IsPublished))
            .Where(b => !b.IsRemoved && b.DraftVisibility == DraftVisibility.Public)
            .Where(b => excludeOwnerIds == null || !excludeOwnerIds.Contains(b.AuthorId))
            .OrderByDescending(b => b.CreatedUtc)
            .Page(paging)
            .ProjectTo<BlogModel>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogModel>> GetUserBlogs(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .TagWith("DM.Blog.ListByUser")
            .Include(b => b.Author)
            .Where(b => !b.IsRemoved && b.AuthorId == userId)
            .OrderByDescending(b => b.CreatedUtc)
            .ProjectTo<BlogModel>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlogModel?> Get(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .TagWith("DM.Blog.Get")
            .Include(b => b.Author)
            .Include(b => b.Rubrics.Where(r => !r.IsRemoved))
            .Include(b => b.Assistants)
            .Include(b => b.Tokens.Where(t => !t.IsRemoved))
            .Where(b => b.BlogId == blogId)
            .ProjectTo<BlogModel>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlogModel?> GetByOwnerUsername(string username, CancellationToken ct = default)
    {
        return await _dbContext.Blogs
            .TagWith("DM.Blog.GetByUsername")
            .Include(b => b.Author)
            .Include(b => b.Rubrics.Where(r => !r.IsRemoved))
            .Include(b => b.Assistants)
            .Include(b => b.Tokens.Where(t => !t.IsRemoved))
            .Where(b => b.Author.Username == username)
            .ProjectTo<BlogModel>(_mapper.ConfigurationProvider)
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
    public async Task<IEnumerable<BlogModel>> GetPopularBlogs(
        int count, IReadOnlyCollection<Guid>? excludeOwnerIds = null, CancellationToken ct = default)
    {
        // Count subscribers (from Subscriptions) for popularity
        var blogIds = await _dbContext.Blogs
            .TagWith("DM.Blog.PopularIds")
            .Where(b => !b.IsRemoved && b.DraftVisibility == DraftVisibility.Public)
            .Where(b => excludeOwnerIds == null || !excludeOwnerIds.Contains(b.AuthorId))
            .Select(b => new
            {
                b.BlogId,
                SubscriberCount = _dbContext.Subscriptions
                    .Count(s => s.TargetType == SubscriptionTargetType.Blog && s.TargetId == b.BlogId)
            })
            .OrderByDescending(x => x.SubscriberCount)
            .Take(count)
            .Select(x => x.BlogId)
            .ToListAsync(ct);

        return await _dbContext.Blogs
            .TagWith("DM.Blog.PopularBlogs")
            .Include(b => b.Author)
            .Where(b => blogIds.Contains(b.BlogId))
            .ProjectTo<BlogModel>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
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
    public async Task<IEnumerable<BlogModel>> GetByIds(IEnumerable<Guid> blogIds, CancellationToken ct = default)
    {
        var blogIdList = blogIds.ToList();
        if (blogIdList.Count == 0)
            return [];

        return await _dbContext.Blogs
            .TagWith("DM.Blog.GetByIds")
            .Include(b => b.Author)
            .Include(b => b.Rubrics.Where(r => !r.IsRemoved))
            .Include(b => b.Assistants)
            .Include(b => b.Tokens.Where(t => !t.IsRemoved))
            .Where(b => !b.IsRemoved && blogIdList.Contains(b.BlogId))
            .OrderByDescending(b => b.CreatedUtc)
            .ProjectTo<BlogModel>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<BlogModel> CreateBlog(CreateBlogEntity entity, CancellationToken ct = default)
    {
        var blog = new DbBlog
        {
            BlogId = entity.BlogId,
            AuthorId = entity.OwnerId,
            Title = entity.Title,
            Description = entity.Description,
            DraftVisibility = entity.DraftVisibility,
            CommentsEnabled = entity.CommentsEnabled,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false
        };

        _dbContext.Blogs.Add(blog);
        await _dbContext.SaveChangesAsync(ct);

        return await Get(entity.BlogId, ct) ?? throw new InvalidOperationException("Blog not found after creation");
    }

    /// <inheritdoc />
    public async Task<BlogModel> UpdateBlog(UpdateBlogEntity entity, CancellationToken ct = default)
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
            blog.DeletedAtUtc = DateTimeOffset.UtcNow;
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
            rubric.DeletedAtUtc = DateTimeOffset.UtcNow;
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
            Preview = entity.Preview,
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
            publication.DeletedAtUtc = DateTimeOffset.UtcNow;

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
}
