using System;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Blog.Users;

/// <summary>
/// Blog user DTO
/// </summary>
public class BlogUser
{
    /// <summary>
    /// User information (lightweight reference)
    /// </summary>
    public UserRef User { get; set; } = null!;

    /// <summary>
    /// Role in the blog
    /// </summary>
    public BlogRole Role { get; set; }

    /// <summary>
    /// When the user joined the blog (null if date unavailable, e.g., for mentors/readers)
    /// </summary>
    public DateTimeOffset? JoinedUtc { get; set; }
}
