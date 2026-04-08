using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using DM.Domain.Community.Features.Polls;
using DM.Web.API.Shared.Dto;
using DomainPoll = DM.Domain.Community.Features.Polls.Poll;
using DomainPollOption = DM.Domain.Community.Features.Polls.PollOption;

namespace DM.Web.API.Features.Community.Polls;

/// <inheritdoc />
internal class PollMappingProfile : Profile
{
    private const int MaxVotersInResponse = 15;

    /// <inheritdoc />
    public PollMappingProfile()
    {
        CreateMap<DomainPoll, Poll>()
            .ForMember(d => d.Status, s => s.MapFrom<PollStatusResolver>());
        CreateMap<DomainPollOption, PollOption>()
            .ForMember(d => d.VotesCount, s => s.MapFrom(o => o.UserIds.Count()))
            .ForMember(d => d.Voted, s => s.MapFrom<PollParticipationResolver>())
            .ForMember(d => d.Voters, s => s.MapFrom((src, _, _, ctx) =>
            {
                if (ctx.Items.TryGetValue("IsAnonymous", out var isAnonymousObj) && isAnonymousObj is true)
                    return null;

                if (!ctx.Items.TryGetValue("VotersByOptionId", out var votersObj) ||
                    votersObj is not Dictionary<Guid, List<UserRef>> votersDict ||
                    !votersDict.TryGetValue(src.Id, out var voters))
                    return null;

                return voters.Take(MaxVotersInResponse);
            }))
            .ForMember(d => d.TotalVoters, s => s.MapFrom((src, _, _, ctx) =>
            {
                if (ctx.Items.TryGetValue("IsAnonymous", out var isAnonymousObj) && isAnonymousObj is true)
                    return null;

                var count = src.UserIds.Count();
                return count > MaxVotersInResponse ? count : (int?)null;
            }));

        CreateMap<CreatePollRequest, CreatePoll>();
    }
}
