using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Dto;
using IAwardService = DM.Domain.Community.Features.Awards.IAwardService;

namespace DM.Web.API.Features.Moderation.Awards;

/// <inheritdoc />
internal class UserAwardApiService : IUserAwardApiService
{
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserAwardApiService(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<UserAward>> Grant(string username, GrantUserAwardRequest request)
    {
        var granted = await _awardService.GrantAsync(
            username, request.AwardTypeId, request.ContestSeriesId, request.WorkUrl);
        return new Envelope<UserAward>(_mapper.Map<UserAward>(granted));
    }

    /// <inheritdoc />
    public Task Revoke(Guid awardId) => _awardService.RevokeAsync(awardId);
}
