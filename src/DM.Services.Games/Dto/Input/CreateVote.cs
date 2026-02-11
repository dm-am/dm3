using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Gaming.Dto.Input;

/// <summary>
/// DTO for creating a vote on a post
/// </summary>
public class CreateVote
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Vote sign (positive, negative, neutral)
    /// </summary>
    public VoteSign Sign { get; set; }

    /// <summary>
    /// Vote reason type
    /// </summary>
    public VoteType Type { get; set; }
}
