using System;

namespace DM.Services.Community.BusinessProcesses.Reviews.Reading;

/// <summary>
/// Filter parameters for game reviews
/// </summary>
public class GameReviewFilter
{
    /// <summary>
    /// Filter by review author ID
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Filter by game ID
    /// </summary>
    public Guid? GameId { get; set; }

    /// <summary>
    /// Filter by games where user is GM
    /// </summary>
    public Guid? GmId { get; set; }
}
