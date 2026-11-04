using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Personal.Blacklists;

public class UserBlacklistApiService
{
    private readonly IUserBlacklistService _blacklistService;
    private readonly BlacklistMapper _mapper;

    public UserBlacklistApiService(
        IUserBlacklistService blacklistService,
        BlacklistMapper mapper)
    {
        _blacklistService = blacklistService;
        _mapper = mapper;
    }

    public async Task<(IEnumerable<BlacklistEntry> Entries, PagingInfo Paging)> GetMyBlacklist(PagingQuery query)
    {
        var allEntries = (await _blacklistService.GetMyBlacklist()).ToList();
        var totalCount = allEntries.Count;

        var pagedEntries = allEntries
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(_mapper.ToBlacklistEntry);

        var pagingInfo = new PagingInfo(query.Skip, query.Take, totalCount);

        return (pagedEntries, pagingInfo);
    }

    public async Task<BlacklistSettings> GetSettings()
    {
        var flags = await _blacklistService.GetSettings();
        return BlacklistMapper.ToBlacklistSettings(flags);
    }

    public async Task<BlacklistSettings> UpdateSettings(UpdateBlacklistSettingsRequest request)
    {
        // One flags enum: a partial update is folded onto the current value.
        var current = await _blacklistService.GetSettings();
        var result = await _blacklistService.UpdateSettings(request.ApplyTo(current));
        return BlacklistMapper.ToBlacklistSettings(result);
    }

    public async Task<BlacklistEntry> BlockUser(BlockUserRequest request) =>
        _mapper.ToBlacklistEntry(await _blacklistService.Block(new OperateUserBlacklistLink { Username = request.Username }));

    public Task UnblockUser(string username) =>
        _blacklistService.Unblock(new OperateUserBlacklistLink { Username = username });
}
