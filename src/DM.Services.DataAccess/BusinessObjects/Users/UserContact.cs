using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Services.DataAccess.BusinessObjects.Users;

/// <summary>
/// User contact information (flexible type+value pairs)
/// </summary>
[Table("UserContacts")]
public class UserContact
{
    /// <summary>
    /// Contact record identifier
    /// </summary>
    [Key]
    public Guid UserContactId { get; set; }

    /// <summary>
    /// Owner user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Contact type (e.g., "Telegram", "Discord", "VK", "Email")
    /// </summary>
    [MaxLength(50)]
    public string ContactType { get; set; } = string.Empty;

    /// <summary>
    /// Contact value (e.g., username, URL, email)
    /// </summary>
    [MaxLength(200)]
    public string ContactValue { get; set; } = string.Empty;

    /// <summary>
    /// Display order (lower = first)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Navigation to user
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
