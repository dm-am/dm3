using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Users.LoginHistory;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class LoginHistoryApiService : ILoginHistoryApiService
{
    private readonly ILoginHistoryService _loginHistoryService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public LoginHistoryApiService(
        ILoginHistoryService loginHistoryService,
        IMapper mapper)
    {
        _loginHistoryService = loginHistoryService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<LoginHistoryDto>> GetByLogin(string login)
    {
        var entries = await _loginHistoryService.GetByLogin(login);
        return new ListEnvelope<LoginHistoryDto>(entries.Select(_mapper.Map<LoginHistoryDto>));
    }
}
