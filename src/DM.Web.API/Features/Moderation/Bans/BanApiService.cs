using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Bans;
using DM.Web.API.Features.Moderation.Warnings;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Bans;

public class BanApiService
{
    private readonly IBanService _banService;
    private readonly WarningMapper _mapper;

    public BanApiService(IBanService banService, WarningMapper mapper)
    {
        _banService = banService;
        _mapper = mapper;
    }

    public async Task<UserBanStatus> GetUserBanStatus(string login)
    {
        var bans = await _banService.GetUserBans(login);
        var activeBan = await _banService.GetActiveBan(login);

        return new UserBanStatus
        {
            Username = login,
            IsBanned = activeBan != null,
            ActiveBan = activeBan != null ? _mapper.ToBan(activeBan) : null,
            History = bans.Select(_mapper.ToBan)
        };
    }

    /// <remarks>
    /// A lifted ban stays out of the public history: the profile counts what the user
    /// served, by the rule of the achievement counter (UserRepository, bansReceivedCounts).
    /// </remarks>
    public async Task<PublicUserBanStatus> GetPublicUserBanStatus(string login)
    {
        var bans = await _banService.GetUserBans(login);
        var activeBan = await _banService.GetActiveBan(login);

        return new PublicUserBanStatus
        {
            Username = login,
            IsBanned = activeBan != null,
            ActiveBan = activeBan != null ? _mapper.ToPublicBan(activeBan) : null,
            History = bans.Where(b => !b.IsLifted).Select(_mapper.ToPublicBan)
        };
    }

    public async Task<Envelope<PublicBan>?> GetActiveBan(string login)
    {
        var ban = await _banService.GetActiveBan(login);
        return ban != null ? new Envelope<PublicBan>(_mapper.ToPublicBan(ban)) : null;
    }

    public async Task<ListEnvelope<Ban>> GetAllActiveBans(BanType? type = null)
    {
        var bans = await _banService.GetAllActiveBans();
        var mapped = bans.Select(_mapper.ToBan);

        // The type is derived during mapping, so the filter runs after it.
        if (type.HasValue)
        {
            mapped = mapped.Where(b => b.Type == type.Value);
        }

        return new ListEnvelope<Ban>(mapped);
    }

    public async Task<ListEnvelope<BanLogEntry>> GetBanHistory(BansQuery query)
    {
        var (entries, totalCount, skip) =
            await _banService.GetBanHistory(query.Username, query.Skip, query.Take, query.Entry);
        return new ListEnvelope<BanLogEntry>(
            entries.Select(_mapper.ToBanLogEntry).ToList(),
            new PagingInfo(skip, query.Take, totalCount));
    }

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
        return new Envelope<Ban>(_mapper.ToBan(ban));
    }

    public Task LiftBan(Guid banId, LiftBanRequest request) => _banService.LiftBan(banId, request.Reason);
}
