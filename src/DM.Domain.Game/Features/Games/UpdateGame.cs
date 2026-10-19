using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

public class UpdateGame
{
    public Guid GameId { get; set; }

    public bool? IsRecruitmentOpen { get; set; }

    public int? RecruitmentPcLimit { get; set; }

    public string? Title { get; set; }

    public string? SystemName { get; set; }

    public string? NarrativeSetting { get; set; }

    public string? Info { get; set; }

    public bool? HideDiceResult { get; set; }

    public bool? ShowPrivateMessages { get; set; }

    public bool? HidePostStats { get; set; }

    public CommentsAccessMode? CommentsAccessMode { get; set; }

    /// <summary>
    /// Short ids, as on creation; null leaves the tags alone, an empty list clears them
    /// </summary>
    public IEnumerable<int>? Tags { get; set; }
}
