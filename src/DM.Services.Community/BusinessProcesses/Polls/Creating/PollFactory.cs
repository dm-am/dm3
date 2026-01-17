using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Boards;

namespace DM.Services.Community.BusinessProcesses.Polls.Creating;

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
    public Poll Create(CreatePoll createPoll)
    {
        return new Poll
        {
            Id = _guidFactory.Create(),
            StartDate = _dateTimeProvider.Now.UtcDateTime,
            EndDate = createPoll.EndDate.UtcDateTime,
            Global = true,
            Title = createPoll.Title,
            Options = createPoll.Options
                .Select(o => new PollOption
                {
                    Id = _guidFactory.Create(),
                    Text = o,
                    UserIds = new List<Guid>()
                })
                .ToList()
        };
    }
}