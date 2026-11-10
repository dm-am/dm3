using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbRubric = DM.Infrastructure.Persistence.Entities.Blog.Rubric;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

internal partial class BlogRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<Rubric>> GetRubrics(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.Rubrics
            .TagWith("DM.Blog.ListRubrics")
            .Where(r => !r.IsRemoved && r.BlogId == blogId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .ProjectToRubric()
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

        var rubric = dbRubric.ToRubric();
        return (rubric, dbRubric.BlogId);
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
            .ProjectToRubric()
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
            .ProjectToRubric()
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReorderRubrics(
        Guid blogId, IReadOnlyList<Guid> orderedRubricIds, CancellationToken ct = default)
    {
        var rubrics = await _dbContext.Rubrics
            .Where(r => r.BlogId == blogId && !r.IsRemoved)
            .ToListAsync(ct);

        for (var i = 0; i < orderedRubricIds.Count; i++)
        {
            var rubric = rubrics.FirstOrDefault(r => r.RubricId == orderedRubricIds[i]);
            if (rubric != null)
            {
                rubric.SortOrder = i;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, Guid[]>> GetRubricPublicationIds(
        Guid blogId, CancellationToken ct = default)
    {
        var rows = await _dbContext.Publications
            .TagWith("DM.Blog.RubricPublicationIds")
            .Where(p => !p.IsRemoved && p.BlogId == blogId && p.RubricId != null)
            .Select(p => new { RubricId = p.RubricId!.Value, p.PublicationId })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.RubricId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PublicationId).ToArray());
    }

    /// <inheritdoc />
    public async Task DeleteRubric(Guid rubricId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var rubric = await _dbContext.Rubrics.FirstOrDefaultAsync(r => r.RubricId == rubricId, ct);
        if (rubric != null)
        {
            SoftDelete.Mark(rubric, deletedByUserId, _dateTimeProvider.Now);
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
