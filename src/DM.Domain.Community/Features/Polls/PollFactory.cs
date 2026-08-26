using System.Linq;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class PollFactory : IPollFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public PollFactory(IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public CreatePollEntity Create(CreatePoll createPoll)
    {
        return new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = createPoll.StartsUtc.UtcDateTime,
            EndsUtc = createPoll.EndsUtc.UtcDateTime,
            Title = createPoll.Title,
            Details = createPoll.Details,
            IsAnonymous = createPoll.IsAnonymous,
            Options = createPoll.Options
                .Select(o => new CreatePollOptionEntity
                {
                    Id = _guidFactory.Create(),
                    Text = o
                })
                .ToList()
        };
    }
}
