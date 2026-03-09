using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Query parameters for game list
/// </summary>
public class GamesQuery : PagingQuery
{
    /// <summary>
    /// Game statuses to include
    /// </summary>
    public IReadOnlyCollection<ModuleStatus> Statuses { get; set; } = [ModuleStatus.Active];

    /// <summary>
    /// Filter by tag ID
    /// </summary>
    public Guid? TagId { get; set; }

    /// <summary>
    /// Filter by recruitment status
    /// </summary>
    public bool? IsRecruiting { get; set; }

    /// <summary>
    /// Filter by finished status
    /// </summary>
    public bool? IsFinished { get; set; }

    /// <summary>
    /// Filter by master username
    /// </summary>
    public string? MasterUsername { get; set; }

    /// <summary>
    /// Filter by player username
    /// </summary>
    public string? PlayerUsername { get; set; }

    /// <summary>
    /// Exclude games by master IDs
    /// </summary>
    public IReadOnlyCollection<Guid>? ExcludeMasterIds { get; set; }
}
