using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Blacklists;

/// <inheritdoc />
internal class UserBlacklistApiService : IUserBlacklistApiService
{
    private readonly IUserBlacklistService _blacklistService;
    private readonly BlacklistMapper _mapper;

    /// <inheritdoc />
    public UserBlacklistApiService(
        IUserBlacklistService blacklistService,
        BlacklistMapper mapper)
    {
        _blacklistService = blacklistService;
        _mapper = mapper;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<BlacklistSettings> GetSettings()
    {
        var flags = await _blacklistService.GetSettings();
        return _mapper.ToBlacklistSettings(flags);
    }

    /// <inheritdoc />
    public async Task<BlacklistSettings> UpdateSettings(UpdateBlacklistSettingsRequest request)
    {
        // The domain stores one flags enum, so a partial update has to be folded
        // onto the current value — otherwise every flag the request omits is
        // written as cleared.
        var current = await _blacklistService.GetSettings();
        var result = await _blacklistService.UpdateSettings(request.ApplyTo(current));
        return _mapper.ToBlacklistSettings(result);
    }

    /// <inheritdoc />
    public async Task<BlacklistEntry> BlockUser(BlockUserRequest request)
    {
        var dto = new OperateUserBlacklistLink { Username = request.Username };
        var entry = await _blacklistService.Block(dto);
        return _mapper.ToBlacklistEntry(entry);
    }

    /// <inheritdoc />
    public async Task UnblockUser(string username)
    {
        var dto = new OperateUserBlacklistLink { Username = username };
        await _blacklistService.Unblock(dto);
    }
}
