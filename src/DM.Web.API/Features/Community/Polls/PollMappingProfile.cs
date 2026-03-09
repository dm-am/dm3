using System.Linq;
using AutoMapper;
using DM.Domain.Community.Features.Polls;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;
using DomainPollOption = DM.Domain.Community.Features.Polls.PollOption;

namespace DM.Web.API.Features.Community.Polls;

/// <inheritdoc />
internal class PollMappingProfile : Profile
{
    /// <inheritdoc />
    public PollMappingProfile()
    {
        CreateMap<DomainPoll, Poll>()
            .ForMember(d => d.EndsUtc, s => s.MapFrom(p => p.EndDate));
        CreateMap<DomainPollOption, PollOption>()
            .ForMember(d => d.VotesCount, s => s.MapFrom(o => o.UserIds.Count()))
            .ForMember(d => d.Voted, s => s.MapFrom<PollParticipationResolver>());

        CreateMap<Poll, CreatePoll>()
            .ForMember(d => d.Title, s => s.MapFrom(p => p.Title))
            .ForMember(d => d.EndDate, s => s.MapFrom(p => p.EndsUtc))
            .ForMember(d => d.Options, s => s.MapFrom(p => p.Options.Select(o => o.Text)));
    }
}
