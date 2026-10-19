using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Identity;

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
    /// How many rows, posts, comments or messages a page of every paged list holds
    /// </summary>
    /// <remarks>One number for every list, as on DM2: the choices are PagingPolicy.AllowedPageSizes.</remarks>
    public int PageSize { get; set; }

    /// <summary>
    /// IANA name of the reader's time zone ("Europe/Moscow"), for the letters.
    /// </summary>
    /// <remarks>
    /// Null means UTC. Never written by a login: the letter warning about a login
    /// would then be stamped in the hours of whoever it warns about.
    /// </remarks>
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// Default user settings for a guest or a newbie
    /// </summary>
    /// <remarks>
    /// A fresh instance per call, not a shared one: this object is handed to real
    /// request identities whenever a user has no settings document, and it is
    /// mutable: one request writing to it would change the defaults every other
    /// request sees.
    /// </remarks>
    public static UserSettings Default => new()
    {
        PageSize = PagingPolicy.DefaultPageSize,
        Theme = Theme.Light
    };
}
