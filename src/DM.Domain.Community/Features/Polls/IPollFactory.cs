
namespace DM.Domain.Community.Features.Polls;

/// <summary>
/// Factory for poll data
/// </summary>
internal interface IPollFactory
{
    /// <summary>
    /// Create new poll data
    /// </summary>
    /// <param name="createPoll"></param>
    /// <returns></returns>
    CreatePollEntity Create(CreatePoll createPoll);
}