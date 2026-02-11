using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Blacklist;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Blacklist;
using DM.Web.API.Dto.Contracts;

namespace DM.Web.API.Services.Blacklist;

/// <inheritdoc />
internal class BlacklistApiService : IBlacklistApiService
{
    private readonly IUserBlacklistService _blacklistService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BlacklistApiService(
        IUserBlacklistService blacklistService,
        IMapper mapper)
    {
        _blacklistService = blacklistService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<BlacklistEntry>> GetMyBlacklist()
    {
        var entries = await _blacklistService.GetMyBlacklist();
        return new ListEnvelope<BlacklistEntry>(entries.Select(_mapper.Map<BlacklistEntry>));
    }

    /// <inheritdoc />
    public async Task<Envelope<UserBlacklistSettings>> GetSettings()
    {
        return new Envelope<UserBlacklistSettings>(await _blacklistService.GetSettings());
    }

    /// <inheritdoc />
    public async Task<Envelope<UserBlacklistSettings>> UpdateSettings(UserBlacklistSettings settings)
    {
        return new Envelope<UserBlacklistSettings>(await _blacklistService.UpdateSettings(settings));
    }

    /// <inheritdoc />
    public async Task<Envelope<BlacklistEntry>> BlockUser(BlockUserRequest request)
    {
        var entry = await _blacklistService.BlockUser(request.Login, request.Reason);
        return new Envelope<BlacklistEntry>(_mapper.Map<BlacklistEntry>(entry));
    }

    /// <inheritdoc />
    public async Task UnblockUser(string login)
    {
        await _blacklistService.UnblockUser(login);
    }
}
