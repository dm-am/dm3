using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbBlogAssistant = DM.Infrastructure.Persistence.Entities.Blog.BlogAssistant;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <summary>
/// The assistants of a blog: the roster with join dates, and adding and removing one
/// </summary>
internal partial class BlogRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetAssistantsWithJoinDate(Guid blogId, CancellationToken ct = default)
    {
        return await _dbContext.BlogAssistants
            .TagWith("DM.Blog.AssistantsWithJoinDate")
            .Where(a => a.BlogId == blogId)
            .SelectSpliced(a => new BlogUser
            {
                User = GeneralUserProjections.RosterCard.Splice(a.User),
                Role = BlogRole.Assistant,
                JoinedUtc = a.JoinedUtc
            })
            .ToListAsync(ct);
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
            .FirstOrDefaultAsync(a => a.BlogId == blogId && a.User.Username.ToLower() == usernameLower, ct);

        if (assistant == null)
        {
            return false;
        }

        _dbContext.BlogAssistants.Remove(assistant);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}
