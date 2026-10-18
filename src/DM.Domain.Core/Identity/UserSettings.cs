using System;
using DM.Domain.Core.Dto;
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
    /// IANA name of the reader's time zone ("Europe/Moscow"), for the letters.
    /// </summary>
    /// <remarks>
    /// The site formats a date in the browser, which knows where its reader is.
    /// A letter has no browser, so this is where the zone comes from. Null means
    /// UTC, which is what every account had before there was a zone to keep.
    ///
    /// Written at registration, and after that only when the reader says so -
    /// never by a login. A login that moves it is a login the account owner did
    /// not necessarily make, and the letter warning them about it would then be
    /// stamped in the hours of whoever it warns about.
    /// </remarks>
    public string? TimeZoneId { get; set; }

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
            TopicsPerPage = PagingPolicy.DefaultPageSize,
            CommentsPerPage = PagingPolicy.DefaultPageSize,
            PostsPerPage = PagingPolicy.DefaultPageSize,
            MessagesPerPage = PagingPolicy.DefaultPageSize,
            EntitiesPerPage = PagingPolicy.DefaultPageSize
        },
        Theme = Theme.Light
    };
}
