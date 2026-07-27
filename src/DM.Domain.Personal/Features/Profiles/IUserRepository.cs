using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Users;

namespace DM.Domain.Personal.Features.Profiles;

/// <summary>
/// Full user repository with read and write operations.
/// Extends IUserReadRepository from Domain.Core with Personal-specific write methods.
/// </summary>
public interface IUserRepository : IUserReadRepository
{
    // ═══ READ (Personal-specific) ═══

    /// <summary>
    /// Get user details by email (for account operations)
    /// </summary>
    Task<UserDetails?> GetUserDetailsByEmail(string email);

    /// <summary>
    /// Get count of post reviews given by user
    /// </summary>
    Task<int> GetPostReviewsGivenCount(Guid userId);

    // ═══ WRITE ═══

    /// <summary>
    /// Update user and settings
    /// </summary>
    Task UpdateUser(UpdateUserEntity userUpdate, UpdateUserSettingsEntity settingsUpdate);

    /// <summary>
    /// Replace user contacts
    /// </summary>
    Task ReplaceUserContacts(Guid userId, IEnumerable<UserContactEntity> contacts);

    /// <summary>
    /// Get confirmed avatar upload ID for user
    /// </summary>
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
    public DM.Domain.Core.Dto.Optional<bool>? RatingDisabled { get; set; }

    /// <summary>
    /// Show birthday to other users
    /// </summary>
    public DM.Domain.Core.Dto.Optional<bool>? ShowBirthday { get; set; }

    /// <summary>
    /// Upload ID for new avatar
    /// </summary>
    public DM.Domain.Core.Dto.Optional<Guid>? AvatarUploadId { get; set; }
}

/// <summary>
/// DTO for updating user settings in MongoDB
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
    public DM.Domain.Core.Dto.Optional<DM.Domain.Core.Enums.Theme>? Theme { get; set; }

    /// <summary>
    /// Comments per page setting
    /// </summary>
    public DM.Domain.Core.Dto.Optional<int>? CommentsPerPage { get; set; }

    /// <summary>
    /// Topics per page setting
    /// </summary>
    public DM.Domain.Core.Dto.Optional<int>? TopicsPerPage { get; set; }

    /// <summary>
    /// Messages per page setting
    /// </summary>
    public DM.Domain.Core.Dto.Optional<int>? MessagesPerPage { get; set; }

    /// <summary>
    /// Posts per page setting
    /// </summary>
    public DM.Domain.Core.Dto.Optional<int>? PostsPerPage { get; set; }

    /// <summary>
    /// Entities per page setting
    /// </summary>
    public DM.Domain.Core.Dto.Optional<int>? EntitiesPerPage { get; set; }
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

    /// <summary>
    /// Contact value
    /// </summary>
    public string ContactValue { get; set; } = null!;

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}
