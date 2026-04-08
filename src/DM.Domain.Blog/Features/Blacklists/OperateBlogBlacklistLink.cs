using System;

namespace DM.Domain.Blog.Features.Blacklists;

/// <summary>
/// DTO for blog blacklist operations
/// </summary>
public class OperateBlogBlacklistLink
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// User's display name (unique username)
    /// </summary>
    public string Username { get; set; } = null!;
}
