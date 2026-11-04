using System.Linq;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class PollFactory : IPollFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PollFactory(IGuidFactory guidFactory, IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public CreatePollEntity Create(CreatePoll createPoll)
    {
        // A start CreatePollValidator let in from behind this clock is now.
        var now = _dateTimeProvider.Now;
        return new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = (createPoll.StartsUtc < now ? now : createPoll.StartsUtc).UtcDateTime,
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
