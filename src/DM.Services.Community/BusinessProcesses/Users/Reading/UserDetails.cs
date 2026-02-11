using System;
using System.Collections.Generic;
using DM.Services.Authentication.Dto;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Users.Reading;

/// <summary>
/// DTO model for user additional data
/// </summary>
public class UserDetails : GeneralUser
{
    /// <summary>
    /// Date of user registration
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// User ICQ number (legacy, migrating to Contacts)
    /// </summary>
    public string Icq { get; set; } = null!;

    /// <summary>
    /// User Skype login (legacy, migrating to Contacts)
    /// </summary>
    public string Skype { get; set; } = null!;

    /// <summary>
    /// User contact information (flexible type+value pairs)
    /// </summary>
    public IReadOnlyCollection<UserContactDto> Contacts { get; set; } = Array.Empty<UserContactDto>();

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
public class UserContactDto
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