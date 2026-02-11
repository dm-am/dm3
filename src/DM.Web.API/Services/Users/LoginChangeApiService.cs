using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Users.LoginChange;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class LoginChangeApiService : ILoginChangeApiService
{
    private readonly ILoginChangeService _loginChangeService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public LoginChangeApiService(
        ILoginChangeService loginChangeService,
        IMapper mapper)
    {
        _loginChangeService = loginChangeService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<LoginChangeRequestDto>> Create(CreateLoginChangeRequestDto request)
    {
        var serviceRequest = _mapper.Map<CreateLoginChangeRequest>(request);
        var result = await _loginChangeService.Create(serviceRequest);
        return new Envelope<LoginChangeRequestDto>(_mapper.Map<LoginChangeRequestDto>(result));
    }

    /// <inheritdoc />
    public async Task<Envelope<LoginChangeRequestDto>?> GetCurrentUserRequest()
    {
        var result = await _loginChangeService.GetCurrentUserRequest();
        return result != null
            ? new Envelope<LoginChangeRequestDto>(_mapper.Map<LoginChangeRequestDto>(result))
            : null;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<LoginChangeRequestDto>> GetPendingRequests()
    {
        var requests = await _loginChangeService.GetPendingRequests();
        return new ListEnvelope<LoginChangeRequestDto>(requests.Select(_mapper.Map<LoginChangeRequestDto>));
    }

    /// <inheritdoc />
    public async Task<Envelope<LoginChangeRequestDto>> GetById(Guid requestId)
    {
        var result = await _loginChangeService.GetById(requestId);
        return new Envelope<LoginChangeRequestDto>(_mapper.Map<LoginChangeRequestDto>(result));
    }

    /// <inheritdoc />
    public async Task<Envelope<LoginChangeRequestDto>> Resolve(Guid requestId, ResolveLoginChangeRequestDto resolve)
    {
        var serviceResolve = _mapper.Map<ResolveLoginChangeRequest>(resolve);
        serviceResolve.RequestId = requestId;
        var result = await _loginChangeService.Resolve(serviceResolve);
        return new Envelope<LoginChangeRequestDto>(_mapper.Map<LoginChangeRequestDto>(result));
    }
}
