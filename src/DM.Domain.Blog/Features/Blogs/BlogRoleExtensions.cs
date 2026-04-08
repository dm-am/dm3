using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Extension methods for BlogRole enum
/// </summary>
public static class BlogRoleExtensions
{
    /// <summary>
    /// Convert BlogRole to API string representation
    /// </summary>
    public static string ToApiString(this BlogRole role) => role switch
    {
        BlogRole.Author => "author",
        BlogRole.Mentor => "mentor",
        BlogRole.Assistant => "assistant",
        BlogRole.Reader => "reader",
        _ => "unknown"
    };
}
