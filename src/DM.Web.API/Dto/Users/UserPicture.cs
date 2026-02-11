using System;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user profile picture
/// </summary>
public class UserPicture
{
    /// <summary>
    /// Picture identifier (for deletion, only in UserDetails context)
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Small picture URL (100x100, used in lists)
    /// </summary>
    public string? SmallUrl { get; set; }

    /// <summary>
    /// Medium picture URL (200x200, used in profiles)
    /// </summary>
    public string? MediumUrl { get; set; }

    /// <summary>
    /// Original picture URL (full size, only in UserDetails context)
    /// </summary>
    public string? OriginalUrl { get; set; }
}
