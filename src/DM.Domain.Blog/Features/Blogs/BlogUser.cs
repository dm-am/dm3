using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Blog user with their role
/// </summary>
public class BlogUser
{
    /// <summary>
    /// User information
    /// </summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// User's role in the blog
    /// </summary>
    public BlogRole Role { get; set; }

    /// <summary>
    /// When the user joined the blog
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }
}
