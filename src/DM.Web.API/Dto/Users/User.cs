using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user
/// </summary>
public class User
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Login
    /// </summary>
    public string Login { get; set; }

    /// <summary>
    /// Roles
    /// </summary>
    public IEnumerable<UserRole> Roles { get; set; }

    /// <summary>
    /// Honorary goblin status
    /// </summary>
    public bool IsHonorary { get; set; }

    /// <summary>
    /// Newbie status (less than 100 posts)
    /// </summary>
    public bool IsNewbie { get; set; }

    /// <summary>
    /// User gender
    /// </summary>
    public Gender Gender { get; set; }

    /// <summary>
    /// Birthday date (day and month only)
    /// </summary>
    public DateOnly? BirthdayDate { get; set; }

    /// <summary>
    /// Profile picture URL M-size
    /// </summary>
    public string MediumPictureUrl { get; set; }

    /// <summary>
    /// Profile picture URL S-size
    /// </summary>
    public string SmallPictureUrl { get; set; }

    /// <summary>
    /// Rating
    /// </summary>
    public Rating Rating { get; set; }

    /// <summary>
    /// Last seen online moment (UTC)
    /// </summary>
    public DateTimeOffset? OnlineUtc { get; set; }

    /// <summary>
    /// User registration moment (UTC)
    /// </summary>
    public DateTimeOffset? RegistrationDateUtc { get; set; }

    /// <summary>
    /// User access policy (ban status)
    /// </summary>
    public AccessPolicy AccessPolicy { get; set; }
}

/// <summary>
/// DTO model for user rating
/// </summary>
public class Rating
{
    /// <summary>
    /// Rating participation flag
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Total quality rating
    /// </summary>
    public int TotalRating { get; set; }

    /// <summary>
    /// Total posts count
    /// </summary>
    public int TotalPosts { get; set; }
}
