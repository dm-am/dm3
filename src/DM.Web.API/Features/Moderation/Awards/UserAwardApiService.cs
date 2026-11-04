using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Dto;
using IAwardService = DM.Domain.Community.Features.Awards.IAwardService;

namespace DM.Web.API.Features.Moderation.Awards;

public class UserAwardApiService
{
    private readonly IAwardService _awardService;
    private readonly AwardMapper _mapper;

    public UserAwardApiService(IAwardService awardService, AwardMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    public async Task<Envelope<UserAward>> Grant(string username, GrantUserAwardRequest request)
    {
        var granted = await _awardService.GrantAsync(
            username, request.AwardTypeId, request.ContestSeriesId, request.WorkUrl);
        return new Envelope<UserAward>(_mapper.ToUserAward(granted));
    }

    public async Task Revoke(string username, Guid awardId)
    {
        // The route names the holder: another user's award answers as an unknown one.
        var held = await _awardService.GetUserAwardsAsync(username);
        if (held.All(award => award.Id != awardId))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.AwardNotFound);
        }

        await _awardService.RevokeAsync(awardId);
    }
}
