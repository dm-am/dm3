using System;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Filter parameters for user endorsements
/// </summary>
public class UserEndorsementFilter
{
    /// <summary>
    /// Filter by endorsement author
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Filter by endorsement recipient (target user)
    /// </summary>
    public Guid? RecipientId { get; set; }
}
