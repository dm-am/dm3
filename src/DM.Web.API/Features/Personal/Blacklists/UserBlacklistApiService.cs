using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Blacklists;

/// <inheritdoc />
internal class UserBlacklistApiService : IUserBlacklistApiService
{
    private readonly IUserBlacklistService _blacklistService;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserBlacklistApiService(
        IUserBlacklistService blacklistService,
        IUserService userService,
        IMapper mapper)
    {
        _blacklistService = blacklistService;
        _userService = userService;
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
            .Select(_mapper.Map<BlacklistEntry>);

        var pagingInfo = new PagingInfo(query.Skip, query.Take, totalCount);

        return (pagedEntries, pagingInfo);
    }

    /// <inheritdoc />
    public async Task<BlacklistSettings> GetSettings()
    {
        var flags = await _blacklistService.GetSettings();
        return _mapper.Map<BlacklistSettings>(flags);
    }

    /// <inheritdoc />
    public async Task<BlacklistSettings> UpdateSettings(BlacklistSettings settings)
    {
        var flags = _mapper.Map<UserBlacklistSettings>(settings);
        var result = await _blacklistService.UpdateSettings(flags);
        return _mapper.Map<BlacklistSettings>(result);
    }

    /// <inheritdoc />
    public async Task<BlacklistEntry> BlockUser(BlockUserRequest request)
    {
        var entry = await _blacklistService.BlockUser(request.Username);
        return _mapper.Map<BlacklistEntry>(entry);
    }

    /// <inheritdoc />
    public async Task UnblockUser(string username)
    {
        await _blacklistService.UnblockUser(username);
    }

    /// <inheritdoc />
    public async Task<bool> CanMessage(string username)
    {
        var targetUser = await _userService.Get(username);
        return await _blacklistService.CanSendMessage(targetUser.UserId);
    }
}
