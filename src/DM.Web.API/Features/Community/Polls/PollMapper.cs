using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;
using DomainPollOption = DM.Domain.Community.Features.Polls.PollOption;
using DomainPollsQuery = DM.Domain.Community.Features.Polls.PollsQuery;
using DomainCreatePoll = DM.Domain.Community.Features.Polls.CreatePoll;
using DomainUpdatePoll = DM.Domain.Community.Features.Polls.UpdatePoll;

namespace DM.Web.API.Features.Community.Polls;

/// <summary>
/// Compile-time mapper for polls. The viewer-dependent fields the AutoMapper
/// profile filled through resolvers and context items are ordinary methods
/// here: the clock and the identity come through the constructor, the voter
/// batch comes as an argument.
/// </summary>
[Mapper]
internal partial class PollMapper
{
    private const int MaxVotersInResponse = 15;

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIdentityProvider _identityProvider;

    public PollMapper(
        IDateTimeProvider dateTimeProvider,
        IIdentityProvider identityProvider)
    {
        _dateTimeProvider = dateTimeProvider;
        _identityProvider = identityProvider;
    }

    /// <summary>
    /// Domain poll to the API poll. <paramref name="votersByOptionId"/> is the
    /// pre-mapped voter batch for public polls; null hides voter lists, the way
    /// an anonymous poll always does.
    /// </summary>
    public Poll ToPoll(DomainPoll poll, IReadOnlyDictionary<Guid, List<UserRef>>? votersByOptionId)
    {
        var result = ToPollCore(poll);
        // The status window lives in the domain; see PollStatusResolver's old
        // remark - a copy of the boundary here would drift from the vote check.
        result.Status = poll.StatusAt(_dateTimeProvider.Now);
        result.Options = poll.Options
            .Select(option => ToPollOption(option, poll.IsAnonymous, votersByOptionId))
            .ToList();
        return result;
    }

    [MapperIgnoreTarget(nameof(Poll.Status))]
    [MapperIgnoreTarget(nameof(Poll.Options))]
    [MapperIgnoreSource(nameof(DomainPoll.Options))]
    private partial Poll ToPollCore(DomainPoll poll);

    /// <summary>
    /// Listing query to the domain one: the same eleven members by name.
    /// </summary>
    public partial DomainPollsQuery ToDomainQuery(PollsQuery query);

    /// <summary>
    /// Create request to the domain DTO.
    /// </summary>
    /// <remarks>
    /// The option list is carried over by the caller rather than mapped, so it
    /// stays the very list the request arrived with - a generated copy would be
    /// equal but not the same object, and equality is not what the hand-written
    /// assignment this replaced promised.
    /// </remarks>
    [MapperIgnoreTarget(nameof(DomainCreatePoll.Options))]
    [MapperIgnoreSource(nameof(CreatePollRequest.Options))]
    public partial DomainCreatePoll ToCreatePoll(CreatePollRequest request);

    /// <summary>
    /// Update request to the domain DTO. The identifier comes from the route and
    /// not from the body, so it is assigned after the map.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdatePoll.Id))]
    public partial DomainUpdatePoll ToUpdatePoll(UpdatePollRequest request);

    // Every member except Id and Text depends on the viewer or the voter
    // batch, so the option is built by hand rather than generated.
    private PollOption ToPollOption(
        DomainPollOption option,
        bool isAnonymous,
        IReadOnlyDictionary<Guid, List<UserRef>>? votersByOptionId)
    {
        var currentUser = _identityProvider.Current.User;
        var votesCount = option.UserIds.Count();

        return new PollOption
        {
            Id = option.Id,
            Text = option.Text,
            VotesCount = votesCount,
            Voted = currentUser.IsAuthenticated
                ? option.UserIds.Contains(currentUser.UserId)
                : null,
            Voters = !isAnonymous &&
                     votersByOptionId != null &&
                     votersByOptionId.TryGetValue(option.Id, out var voters)
                ? voters.Take(MaxVotersInResponse)
                : null,
            TotalVoters = !isAnonymous && votesCount > MaxVotersInResponse
                ? votesCount
                : null,
        };
    }
}
