using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Output DTO for ban
/// </summary>
public class Ban
{
    /// <summary>
    /// Ban identifier
    /// </summary>
    public Guid BanId { get; set; }

    /// <summary>
    /// Target user
    /// </summary>
    public GeneralUser TargetUser { get; set; } = null!;

    /// <summary>
    /// Ban author (moderator or user for voluntary bans)
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Ban start time
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Ban end time
    /// </summary>
    public DateTimeOffset EndedUtc { get; set; }

    /// <summary>
    /// Ban comment/reason
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// Access restriction policy
    /// </summary>
    public AccessPolicy AccessRestrictionPolicy { get; set; }

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }

    /// <summary>
    /// Whether the ban has been removed (lifted early)
    /// </summary>
    public bool IsRemoved { get; set; }
}

/// <summary>
/// Entity DTO for creating a ban (repository level)
/// </summary>
public class CreateBanEntity
{
    /// <summary>
    /// Ban identifier
    /// </summary>
    public Guid BanId { get; set; }

    /// <summary>
    /// Target user identifier
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Ban start time
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Ban end time
    /// </summary>
    public DateTimeOffset EndedUtc { get; set; }

    /// <summary>
    /// Ban comment
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// Access restriction policy
    /// </summary>
    public AccessPolicy AccessRestrictionPolicy { get; set; }

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }
}
