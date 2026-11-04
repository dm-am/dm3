using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Bans;

/// <summary>
/// Entity DTO for creating a ban (repository level)
/// </summary>
public class CreateBanEntity
{
    public Guid BanId { get; set; }

    public Guid TargetUserId { get; set; }

    public Guid AuthorId { get; set; }

    public DateTimeOffset StartedUtc { get; set; }

    public DateTimeOffset EndedUtc { get; set; }

    public string Comment { get; set; } = "";

    public AccessPolicy AccessRestrictionPolicy { get; set; }

    /// <summary>
    /// Whether this is a voluntary self-ban
    /// </summary>
    public bool IsVoluntary { get; set; }
}
