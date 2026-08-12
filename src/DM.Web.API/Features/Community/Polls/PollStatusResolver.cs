using AutoMapper;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;

namespace DM.Web.API.Features.Community.Polls;

/// <summary>
/// Reports the poll status the domain derives from the poll's two dates.
/// </summary>
/// <remarks>
/// The window itself is not spelled out here, and it used to be. The same window
/// decides whether a vote is accepted, so a boundary changed in one copy and not
/// the other would have this call a poll open while the vote it invites is
/// refused.
/// </remarks>
internal class PollStatusResolver : IValueResolver<DomainPoll, Poll, PollStatus>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PollStatusResolver(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public PollStatus Resolve(DomainPoll source, Poll destination, PollStatus destMember, ResolutionContext context) =>
        source.StatusAt(_dateTimeProvider.Now);
}
