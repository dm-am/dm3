using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using AwesomeAssertions;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

// The storage filter and ModuleVisibility.IsPubliclyVisible answer one question twice, because EF
// translates expression trees and not method calls; every combination is compared.
public class BlogAccessibilityFiltersShould
{
    [Theory]
    [MemberData(nameof(GameAccessibilityFiltersShould.ModuleStates), MemberType = typeof(GameAccessibilityFiltersShould))]
    public void ListTheBlogExactlyWhenTheDomainRuleCallsItVisible(
        ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        var blogs = new[]
        {
            new DbBlog
            {
                Status = status,
                PremoderationStatus = premoderationStatus,
                DraftVisibility = draftVisibility
            }
        }.AsQueryable();

        var listed = blogs.Any(BlogAccessibilityFilters.PubliclyVisible);

        listed.Should().Be(ModuleVisibility.IsPubliclyVisible(status, premoderationStatus, draftVisibility));
    }
}
