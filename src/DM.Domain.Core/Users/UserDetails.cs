using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;

namespace DM.Domain.Core.Users;

/// <summary>
/// DTO model for detailed user data (extends GeneralUser with contacts, info, settings).
/// Used by Personal, Community, and Moderation modules.
/// </summary>
public class UserDetails : GeneralUser
{
    /// <summary>
    /// Date of user registration
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// User contact information (flexible type+value pairs)
    /// </summary>
    public IReadOnlyCollection<UserContact> Contacts { get; set; } = Array.Empty<UserContact>();

    /// <summary>
    /// User-defined extended information
    /// </summary>
    public string Info { get; set; } = null!;

    /// <summary>
    /// User settings
    /// </summary>
    public UserSettings Settings { get; set; } = null!;
}

/// <summary>
/// DTO for a single user contact
/// </summary>
public class UserContact
{
    /// <summary>
    /// Contact type (e.g., "Telegram", "Discord")
    /// </summary>
    public string ContactType { get; set; } = string.Empty;

    /// <summary>
    /// Contact value (e.g., username, URL)
    /// </summary>
    public string ContactValue { get; set; } = string.Empty;

    /// <summary>
    /// Display order
    /// </summary>
    public int SortOrder { get; set; }
}
