using System;
using AutoMapper;
using DbPoll = DM.Services.DataAccess.BusinessObjects.Boards.Poll;
using DbPollOption = DM.Services.DataAccess.BusinessObjects.Boards.PollOption;

namespace DM.Services.Community.BusinessProcesses.Polls.Reading;

/// <inheritdoc />
internal class PollProfile : Profile
{
    /// <inheritdoc />
    public PollProfile()
    {
        CreateMap<DbPoll, Poll>()
            .ForMember(d => d.StartDate, s => s.MapFrom(p => new DateTimeOffset(p.StartDate, TimeSpan.Zero)))
            .ForMember(d => d.EndDate, s => s.MapFrom(p => new DateTimeOffset(p.EndDate, TimeSpan.Zero)));

        CreateMap<DbPollOption, PollOption>();
    }
}
