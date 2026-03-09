using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Input DTO for games filtering
/// </summary>
public class GamesQuery : PagingQuery
{
    /// <summary>
    /// Game statuses to filter by (optional)
    /// </summary>
    public IEnumerable<ModuleStatus>? Statuses { get; set; }

    /// <summary>
    /// Game tags to filter by (optional)
    /// </summary>
    public IEnumerable<Guid>? Tag { get; set; }

    /// <summary>
    /// Filter by recruitment status (optional)
    /// </summary>
    public bool? IsRecruiting { get; set; }

    /// <summary>
    /// Filter by finished status (optional, for closed games)
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
