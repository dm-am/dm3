using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;

namespace DM.Domain.Personal.Features.Profiles;

/// <summary>
/// Full user repository with read and write operations.
/// Extends IUserReadRepository from Domain.Core with Personal-specific write methods.
/// </summary>
public interface IUserRepository : IUserReadRepository
{
    // Write

    Task UpdateUser(UpdateUserEntity userUpdate, UpdateUserSettingsEntity settingsUpdate);

    Task ReplaceUserContacts(Guid userId, IEnumerable<UserContactEntity> contacts);

    Task<Guid?> GetConfirmedAvatarUpload(Guid userId, Guid uploadId);

    /// <summary>
    /// Link upload to user entity and mark old uploads as obsolete
    /// </summary>
    Task LinkAvatarUpload(Guid userId, Guid uploadId);

    /// <summary>
    /// Reset the user's avatar: User.AvatarUploadId = null,
    /// all their UserAvatar uploads are marked IsRemoved=true.
    /// Idempotent: if there is no avatar — no-op.
    /// </summary>
    Task UnlinkAvatarUpload(Guid userId);
}

/// <summary>
/// DTO for updating user entity in database
/// </summary>
public class UpdateUserEntity
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User defined status (null = don't update, empty = clear)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Flag indicating Status should be updated
    /// </summary>
    public bool UpdateStatus { get; set; }

    /// <summary>
    /// User real name (null = don't update, empty = clear)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Flag indicating Name should be updated
    /// </summary>
    public bool UpdateName { get; set; }

    /// <summary>
    /// User real location (null = don't update, empty = clear)
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Flag indicating Location should be updated
    /// </summary>
    public bool UpdateLocation { get; set; }

    /// <summary>
    /// User-defined extended information (null = don't update, empty = clear)
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// Flag indicating Info should be updated
    /// </summary>
    public bool UpdateInfo { get; set; }

    /// <summary>
    /// Rating disability flag
    /// </summary>
    public Optional<bool>? RatingDisabled { get; set; }

    /// <summary>
    /// Show birthday to other users
    /// </summary>
    public Optional<bool>? ShowBirthday { get; set; }

    public Optional<Guid>? AvatarUploadId { get; set; }
}

/// <summary>
/// DTO for updating user settings
/// </summary>
public class UpdateUserSettingsEntity
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Color theme
    /// </summary>
    public Optional<Theme>? Theme { get; set; }

    /// <summary>
    /// Whether the time zone below is part of this update.
    /// </summary>
    /// <remarks>
    /// A flag and not an Optional, which only wraps value types: the zone is a
    /// string, and null is a value it can be set to - the reader who clears it
    /// is asking for UTC, not asking for nothing to change.
    /// </remarks>
    public bool UpdateTimeZone { get; set; }

    /// <summary>
    /// IANA name of the reader's time zone, null for UTC
    /// </summary>
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// Page size of every paged list
    /// </summary>
    public Optional<int>? PageSize { get; set; }
}

/// <summary>
/// DTO for user contact entity
/// </summary>
public class UserContactEntity
{
    /// <summary>
    /// Contact identifier
    /// </summary>
    public Guid UserContactId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Contact type (e.g., "Telegram", "Discord", etc.)
    /// </summary>
    public string ContactType { get; set; } = null!;

    public string ContactValue { get; set; } = null!;

    public int SortOrder { get; set; }
}
