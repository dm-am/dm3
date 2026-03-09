using System.Linq;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class PollFactory : IPollFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PollFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public CreatePollEntity Create(CreatePoll createPoll)
    {
        return new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartDate = _dateTimeProvider.Now.UtcDateTime,
            EndDate = createPoll.EndDate.UtcDateTime,
            Global = true,
            Title = createPoll.Title,
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