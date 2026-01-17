using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.BbRendering;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user details
/// </summary>
public class UserDetails : User
{
    /// <summary>
    /// Profile picture identifier
    /// </summary>
    public Guid? PictureGuid { get; set; }

    /// <summary>
    /// URL of profile picture original
    /// </summary>
    public string OriginalPictureUrl { get; set; }

    /// <summary>
    /// User defined status
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Given post review text
    /// </summary>
    public string GivenPostReview { get; set; }

    /// <summary>
    /// User real name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// User real location
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// User contact information
    /// </summary>
    public IEnumerable<UserContact> Contacts { get; set; }

    /// <summary>
    /// User-defined extended information
    /// </summary>
    public InfoBbText Info { get; set; }

    /// <summary>
    /// User settings
    /// </summary>
    public UserSettings Settings { get; set; }
}

/// <summary>
/// User contact information
/// </summary>
public class UserContact
{
    /// <summary>
    /// Contact type title (e.g., "Telegram", "Discord", "Email")
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Contact value
    /// </summary>
    public string Value { get; set; }
}
