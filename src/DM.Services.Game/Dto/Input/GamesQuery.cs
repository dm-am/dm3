using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.Dto.Input;

/// <summary>
/// Query to search games by
/// </summary>
public class GamesQuery : PagingQuery
{
    /// <summary>
    /// Games should only be in these statuses
    /// </summary>
    public HashSet<ModuleStatus> Statuses { get; set; } = [];

    /// <summary>
    /// Games should contain this tag
    /// </summary>
    public Guid? TagId { get; set; }

    /// <summary>
    /// Filter by recruitment status
    /// </summary>
    public bool? IsRecruiting { get; set; }

    /// <summary>
    /// Filter by finished status (for closed games)
    /// </summary>
    public bool? IsFinished { get; set; }

    /// <summary>
    /// Filter by master login (optional)
    /// </summary>
    public string? MasterLogin { get; set; }

    /// <summary>
    /// Filter by player login (optional, user has active character in game)
    /// </summary>
    public string? PlayerLogin { get; set; }
}