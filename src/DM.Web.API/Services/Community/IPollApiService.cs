using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Community;
using DM.Web.API.Dto.Contracts;

namespace DM.Web.API.Services.Community;

/// <summary>
/// API service for polls
/// </summary>
public interface IPollApiService
{
    /// <summary>
    /// Get polls
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    Task<ListEnvelope<Poll>> Get(PollsQuery query);

    /// <summary>
    /// Get single poll
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<Envelope<Poll>> Get(Guid id);

    /// <summary>
    /// Create new poll
    /// </summary>
    /// <param name="request">Poll creation request</param>
    /// <returns></returns>
    Task<Envelope<Poll>> Create(CreatePollRequest request);

    /// <summary>
    /// Vote for the poll option
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <param name="optionId">Option identifier</param>
    /// <returns></returns>
    Task<Envelope<Poll>> Vote(Guid pollId, Guid optionId);

    /// <summary>
    /// Remove vote from the poll
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    /// <returns></returns>
    Task<Envelope<Poll>> Unvote(Guid pollId);

    /// <summary>
    /// Update existing poll
    /// </summary>
    /// <param name="id">Poll identifier</param>
    /// <param name="request">Poll update request</param>
    /// <returns></returns>
    Task<Envelope<Poll>> Update(Guid id, UpdatePollRequest request);

    /// <summary>
    /// Delete poll (soft delete)
    /// </summary>
    /// <param name="id">Poll identifier</param>
    Task Delete(Guid id);
}