using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Blogs;

/// <summary>
/// Participation role in a blog
/// </summary>
public enum BlogParticipation
{
    /// <summary>
    /// Blog reader (subscribed)
    /// </summary>
    Reader = 0,

    /// <summary>
    /// Blog assistant (can edit publications)
    /// </summary>
    Assistant = 1,

    /// <summary>
    /// Blog mentor (moderates newbie blogs, approves publications)
    /// </summary>
    Mentor = 2,

    /// <summary>
    /// Blog owner
    /// </summary>
    Owner = 3
}

/// <summary>
/// DAL model for blog participant
/// </summary>
[Table("BlogParticipants")]
public class BlogParticipant
{
    /// <summary>
    /// Participant entry identifier
    /// </summary>
    [Key]
    public Guid ParticipantId { get; set; }

    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Participation role
    /// </summary>
    public BlogParticipation Role { get; set; }

    /// <summary>
    /// When user joined
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Blog
    /// </summary>
    [ForeignKey(nameof(BlogId))]
    public virtual Blog Blog { get; set; } = null!;

    /// <summary>
    /// User
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    #endregion
}
