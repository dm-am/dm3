using AutoMapper;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;

namespace DM.Web.API.Features.Community.Polls;

/// <summary>
/// Resolves poll status based on StartsUtc and EndsUtc
/// </summary>
internal class PollStatusResolver : IValueResolver<DomainPoll, Poll, PollStatus>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PollStatusResolver(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public PollStatus Resolve(DomainPoll source, Poll destination, PollStatus destMember, ResolutionContext context)
    {
        var now = _dateTimeProvider.Now;

        if (now < source.StartsUtc)
            return PollStatus.Pending;

        if (now >= source.EndsUtc)
            return PollStatus.Closed;

        return PollStatus.Active;
    }
}
