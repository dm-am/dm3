using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// User display preferences
/// </summary>
/// <remarks>
/// Used for: GET/PATCH /v1/users/me/preferences
/// Controls how user sees the website (not part of profile).
/// Returned in LoginResponse for immediate UI application.
/// </remarks>
public class Preferences
{
    /// <summary>
    /// Website color theme: the one a device takes the first time this account
    /// signs in on it
    /// </summary>
    public Theme Theme { get; set; }

    /// <summary>
    /// How many rows, posts, comments or messages a page of every paged list
    /// holds. One of PagingPolicy.AllowedPageSizes, PagingPolicy.DefaultPageSize
    /// for a reader who never chose one
    /// </summary>
    public int PageSize { get; set; } = PagingPolicy.DefaultPageSize;

    /// <summary>
    /// IANA name of the reader's time zone ("Europe/Moscow"), used by letters
    /// and by the site itself: a signed-in reader sees every date and time on
    /// the site in this zone (a guest, in the zone of the device).
    /// Null or empty means UTC.
    /// </summary>
    public string? TimeZoneId { get; set; }
}
