using System;
using DM.Domain.Core.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Tags;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbTag = DM.Infrastructure.Persistence.Entities.Shared.Tag;
using DbTagGroup = DM.Infrastructure.Persistence.Entities.Shared.TagGroup;
using DtoTag = DM.Domain.Moderation.Features.Tags.Tag;
using DtoTagGroup = DM.Domain.Moderation.Features.Tags.TagGroup;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class TagManagementRepository : ITagManagementRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;

    public TagManagementRepository(DmDbContext dbContext, IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DtoTagGroup>> GetGroups(CancellationToken ct = default)
    {
        return await _dbContext.TagGroups
            .OrderBy(g => g.SortOrder)
            .Select(g => new DtoTagGroup
            {
                Id = g.TagGroupId,
                Title = g.Title,
                Description = g.Description,
                SortOrder = g.SortOrder,
                TagsCount = g.Tags.Count
            })
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DtoTagGroup?> GetGroup(Guid groupId, CancellationToken ct = default)
    {
        return await _dbContext.TagGroups
            .Where(g => g.TagGroupId == groupId)
            .Select(g => new DtoTagGroup
            {
                Id = g.TagGroupId,
                Title = g.Title,
                Description = g.Description,
                SortOrder = g.SortOrder,
                TagsCount = g.Tags.Count
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> GroupTitleExists(string title, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = _dbContext.TagGroups.Where(g => g.Title == title);
        if (excludeId.HasValue)
        {
            query = query.Where(g => g.TagGroupId != excludeId.Value);
        }
        return query.AnyAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DtoTagGroup> CreateGroup(CreateTagGroup createGroup, CancellationToken ct = default)
    {
        var group = new DbTagGroup
        {
            TagGroupId = _guidFactory.Create(),
            Title = createGroup.Title,
            Description = createGroup.Description,
            SortOrder = createGroup.SortOrder
        };

        _dbContext.TagGroups.Add(group);
        await _dbContext.SaveChangesAsync(ct);

        return (await GetGroup(group.TagGroupId, ct))!;
    }

    /// <inheritdoc />
    public async Task<DtoTagGroup> UpdateGroup(UpdateTagGroup updateGroup, CancellationToken ct = default)
    {
        var group = await _dbContext.TagGroups.FindAsync([updateGroup.Id], ct);
        if (group == null)
        {
            throw new InvalidOperationException($"TagGroup {updateGroup.Id} not found");
        }

        group.Title = updateGroup.Title;
        group.Description = updateGroup.Description;
        group.SortOrder = updateGroup.SortOrder;

        await _dbContext.SaveChangesAsync(ct);

        return (await GetGroup(group.TagGroupId, ct))!;
    }

    /// <inheritdoc />
    public async Task DeleteGroup(Guid groupId, CancellationToken ct = default)
    {
        var group = await _dbContext.TagGroups.FindAsync([groupId], ct);
        if (group != null)
        {
            _dbContext.TagGroups.Remove(group);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DtoTag>> GetTags(CancellationToken ct = default)
    {
        return await _dbContext.Tags
            .OrderBy(t => t.TagGroup.SortOrder)
            .ThenBy(t => t.SortOrder)
            .Select(t => new DtoTag
            {
                Id = t.TagId,
                ShortId = t.ShortId,
                GroupId = t.TagGroupId,
                GroupTitle = t.TagGroup.Title,
                Title = t.Title,
                Description = t.Description,
                SortOrder = t.SortOrder,
                GamesCount = t.GameTags.Count
            })
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DtoTag>> GetTagsByGroup(Guid groupId, CancellationToken ct = default)
    {
        return await _dbContext.Tags
            .Where(t => t.TagGroupId == groupId)
            .OrderBy(t => t.SortOrder)
            .Select(t => new DtoTag
            {
                Id = t.TagId,
                ShortId = t.ShortId,
                GroupId = t.TagGroupId,
                GroupTitle = t.TagGroup.Title,
                Title = t.Title,
                Description = t.Description,
                SortOrder = t.SortOrder,
                GamesCount = t.GameTags.Count
            })
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DtoTag?> GetTag(Guid tagId, CancellationToken ct = default)
    {
        return await _dbContext.Tags
            .Where(t => t.TagId == tagId)
            .Select(t => new DtoTag
            {
                Id = t.TagId,
                ShortId = t.ShortId,
                GroupId = t.TagGroupId,
                GroupTitle = t.TagGroup.Title,
                Title = t.Title,
                Description = t.Description,
                SortOrder = t.SortOrder,
                GamesCount = t.GameTags.Count
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> TagTitleExists(string title, Guid groupId, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = _dbContext.Tags.Where(t => t.Title == title && t.TagGroupId == groupId);
        if (excludeId.HasValue)
        {
            query = query.Where(t => t.TagId != excludeId.Value);
        }
        return query.AnyAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DtoTag> CreateTag(CreateTag createTag, CancellationToken ct = default)
    {
        var tag = new DbTag
        {
            TagId = _guidFactory.Create(),
            // Drawn from a sequence rather than computed as MAX + 1 over the table: two
            // creates in one moment read one maximum, and deletion below is physical, so a
            // maximum that walks backwards hands the number of the tag just deleted — the
            // number older links are written in — to the next one. See TagNumbers.
            ShortId = await TagNumbers.NextAsync(_dbContext, ct),
            TagGroupId = createTag.GroupId,
            Title = createTag.Title,
            Description = createTag.Description,
            SortOrder = createTag.SortOrder
        };

        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync(ct);

        return (await GetTag(tag.TagId, ct))!;
    }

    /// <inheritdoc />
    public async Task<DtoTag> UpdateTag(UpdateTag updateTag, CancellationToken ct = default)
    {
        var tag = await _dbContext.Tags.FindAsync([updateTag.Id], ct);
        if (tag == null)
        {
            throw new InvalidOperationException($"Tag {updateTag.Id} not found");
        }

        tag.TagGroupId = updateTag.GroupId;
        tag.Title = updateTag.Title;
        tag.Description = updateTag.Description;
        tag.SortOrder = updateTag.SortOrder;

        await _dbContext.SaveChangesAsync(ct);

        return (await GetTag(tag.TagId, ct))!;
    }

    /// <inheritdoc />
    public async Task DeleteTag(Guid tagId, CancellationToken ct = default)
    {
        var tag = await _dbContext.Tags.FindAsync([tagId], ct);
        if (tag != null)
        {
            _dbContext.Tags.Remove(tag);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public Task<int> GetTagUsageCount(Guid tagId, CancellationToken ct = default)
    {
        return _dbContext.GameTags.CountAsync(gt => gt.TagId == tagId, ct);
    }
}
