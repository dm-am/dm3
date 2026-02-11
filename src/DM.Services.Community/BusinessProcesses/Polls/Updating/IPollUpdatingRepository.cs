using System;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Polls.Reading;

namespace DM.Services.Community.BusinessProcesses.Polls.Updating;

/// <summary>
/// Storage for poll updating
/// </summary>
internal interface IPollUpdatingRepository
{
    /// <summary>
    /// Update poll
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="title">New title (null to keep current)</param>
    /// <param name="endDate">New end date (null to keep current)</param>
    /// <returns></returns>
    Task<Poll> UpdatePoll(Guid pollId, string? title, DateTimeOffset? endDate);
}
