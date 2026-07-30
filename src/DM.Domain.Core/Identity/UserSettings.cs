using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Identity;

/// <summary>
/// User settings
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Settings id (should be same as UserId)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Color theme for the website
    /// </summary>
    public Theme Theme { get; set; }

    /// <summary>
    /// Paging settings
    /// </summary>
    public PagingSettings Paging { get; set; } = null!;

    /// <summary>
    /// Default user settings for a guest or a newbie
    /// </summary>
    /// <remarks>
    /// A fresh instance per call, not a shared one: this object is handed to real
    /// request identities whenever a user has no settings document, and both it and
    /// its <see cref="PagingSettings"/> are mutable — one request writing to it would
    /// change the defaults every other request sees.
    /// </remarks>
    public static UserSettings Default => new()
    {
        Paging = new PagingSettings
        {
            TopicsPerPage = 10,
            CommentsPerPage = 10,
            PostsPerPage = 10,
            MessagesPerPage = 10,
            EntitiesPerPage = 10
        },
        Theme = Theme.Light
    };
}
