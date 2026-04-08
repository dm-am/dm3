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
            .ForMember(d => d.StartsUtc, s => s.MapFrom(p => new DateTimeOffset(p.StartsUtc, TimeSpan.Zero)))
            .ForMember(d => d.EndsUtc, s => s.MapFrom(p => new DateTimeOffset(p.EndsUtc, TimeSpan.Zero)));

        CreateMap<DbPollOption, PollOption>();
    }
}
