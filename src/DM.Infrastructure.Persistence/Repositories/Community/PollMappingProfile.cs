using System;
using AutoMapper;
using DM.Domain.Community.Features.Polls;
using DbPoll = DM.Infrastructure.Persistence.Entities.Forum.Poll;
using DbPollOption = DM.Infrastructure.Persistence.Entities.Forum.PollOption;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class PollMappingProfile : Profile
{
    /// <inheritdoc />
    public PollMappingProfile()
    {
        CreateMap<DbPoll, Poll>()
            .ForMember(d => d.StartDate, s => s.MapFrom(p => new DateTimeOffset(p.StartDate, TimeSpan.Zero)))
            .ForMember(d => d.EndDate, s => s.MapFrom(p => new DateTimeOffset(p.EndDate, TimeSpan.Zero)));

        CreateMap<DbPollOption, PollOption>();
    }
}
