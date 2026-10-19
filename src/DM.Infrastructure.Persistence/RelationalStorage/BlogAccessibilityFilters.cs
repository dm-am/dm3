using System;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// Filters for blog visibility
/// </summary>
public static class BlogAccessibilityFilters
{
    /// <summary>
    /// ModuleVisibility.IsPubliclyVisible as a tree EF can translate.
    /// </summary>
    public static readonly Expression<Func<DbBlog, bool>> PubliclyVisible = blog =>
        blog.PremoderationStatus == PremoderationStatus.Approved &&
        (blog.Status != ModuleStatus.Draft || blog.DraftVisibility == DraftVisibility.Public);
}
