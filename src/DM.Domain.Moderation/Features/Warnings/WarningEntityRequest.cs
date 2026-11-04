using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// One warning's reference to the object it was issued for
/// </summary>
public class WarningEntityRequest
{
    public Guid WarningId { get; set; }

    public Guid EntityId { get; set; }

    public WarningEntityType EntityType { get; set; }
}
