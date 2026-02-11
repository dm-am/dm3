using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Account.Activation;
using DM.Services.Community.BusinessProcesses.Account.Login;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.Core.Authentication;
using DM.Web.Core.Authentication.Credentials;
using Microsoft.AspNetCore.Http;
using ApiActivationRequest = DM.Web.API.Dto.Users.ActivationRequest;
using ServiceActivationRequest = DM.Services.Community.BusinessProcesses.Account.Activation.ActivationRequest;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class ActivationApiService : IActivationApiService
{
    private readonly IActivationService _activationService;
    private readonly ILoginAvailabilityService _loginAvailabilityService;
    private readonly IWebAuthenticationService _webAuthenticationService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ActivationApiService(
        IActivationService activationService,
        ILoginAvailabilityService loginAvailabilityService,
        IWebAuthenticationService webAuthenticationService,
        IMapper mapper)
    {
        _activationService = activationService;
        _loginAvailabilityService = loginAvailabilityService;
        _webAuthenticationService = webAuthenticationService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PendingInfoResponse?> GetPendingInfo(Guid token)
    {
        var result = await _activationService.GetPendingInfo(token);

        if (result == null || result.Status == "not_found")
        {
            return null;
        }

        return new PendingInfoResponse
        {
            Status = result.Status,
            Email = result.Email ?? ""
        };
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> Activate(ApiActivationRequest request, HttpContext httpContext)
    {
        // Map API DTO to service DTO
        var serviceRequest = new ServiceActivationRequest
        {
            Token = request.Token,
            Login = request.Login,
            ExpectedEmail = request.ExpectedEmail
        };

        var userId = await _activationService.Activate(serviceRequest);

        // Auto-login: authenticate and set cookie
        var identity = await _webAuthenticationService.Authenticate(
            new UnconditionalCredentials { UserId = userId },
            httpContext);

        return new Envelope<User>(_mapper.Map<User>(identity.User));
    }

    /// <inheritdoc />
    public async Task<LoginAvailabilityResponse> CheckLoginAvailability(string login)
    {
        var result = await _loginAvailabilityService.CheckAvailability(login);

        return new LoginAvailabilityResponse
        {
            IsAvailable = result.IsAvailable,
            Reason = result.Reason
        };
    }

    /// <inheritdoc />
    public async Task ResendActivation(ResendActivation resendActivation)
    {
        await _activationService.ResendActivation(resendActivation.Email);
    }
}
