using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Moderation.Features.Warnings;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Bans;

/// <inheritdoc />
internal class BanApiService : IBanApiService
{
    private readonly IBanService _banService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BanApiService(IBanService banService, IMapper mapper)
    {
        _banService = banService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<UserBanStatus> GetUserBanStatus(string login)
    {
        var bans = await _banService.GetUserBans(login);
        var activeBan = await _banService.GetActiveBan(login);

        return new UserBanStatus
        {
            Username = login,
            IsBanned = activeBan != null,
            ActiveBan = activeBan != null ? _mapper.Map<Ban>(activeBan) : null,
            History = bans.Select(_mapper.Map<Ban>)
        };
    }

    /// <inheritdoc />
    public async Task<PublicUserBanStatus> GetPublicUserBanStatus(string login)
    {
        var bans = await _banService.GetUserBans(login);
        var activeBan = await _banService.GetActiveBan(login);

        return new PublicUserBanStatus
        {
            Username = login,
            IsBanned = activeBan != null,
            ActiveBan = activeBan != null ? _mapper.Map<PublicBan>(activeBan) : null,
            History = bans.Select(_mapper.Map<PublicBan>)
        };
    }

    /// <inheritdoc />
    public async Task<Envelope<PublicBan>?> GetActiveBan(string login)
    {
        var ban = await _banService.GetActiveBan(login);
        return ban != null ? new Envelope<PublicBan>(_mapper.Map<PublicBan>(ban)) : null;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Ban>> GetAllActiveBans(BanType? type = null)
    {
        var bans = await _banService.GetAllActiveBans();
        var mapped = bans.Select(_mapper.Map<Ban>);

        // The type is derived during mapping, so the filter runs after it. The
        // parameter used to be accepted, documented and ignored: ?type=Permanent
        // answered with every active ban there was.
        if (type.HasValue)
        {
            mapped = mapped.Where(b => b.Type == type.Value);
        }

        return new ListEnvelope<Ban>(mapped);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Ban>> GetBanHistory(PagingQuery query)
    {
        var (bans, totalCount) = await _banService.GetBanHistory(query.Skip, query.Take);
        return new ListEnvelope<Ban>(
            bans.Select(_mapper.Map<Ban>),
            new PagingInfo(query.Skip, query.Take, totalCount));
    }

    /// <inheritdoc />
    public async Task<Envelope<Ban>> CreateBan(CreateBanRequest request)
    {
        var createBan = new CreateBan
        {
            Username = request.Username,
            ExpiresUtc = request.ExpiresUtc,
            DurationHours = request.DurationHours,
            Comment = request.Comment,
            AccessRestrictionPolicy = request.AccessPolicy,
            IsVoluntary = false
        };

        var ban = await _banService.CreateBan(createBan);
        return new Envelope<Ban>(_mapper.Map<Ban>(ban));
    }

    /// <inheritdoc />
    public Task LiftBan(Guid banId, LiftBanRequest? request = null)
    {
        return _banService.LiftBan(banId, request?.Reason);
    }
}
