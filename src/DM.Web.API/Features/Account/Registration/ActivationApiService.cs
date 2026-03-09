using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Registration;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Authentication.Credentials;
using Microsoft.AspNetCore.Http;
using ServiceActivationRequest = DM.Domain.Account.Features.Registration.ActivationRequest;

namespace DM.Web.API.Features.Account.Registration;

/// <inheritdoc />
internal class ActivationApiService : IActivationApiService
{
    private readonly IActivationService _activationService;
    private readonly IWebAuthenticationService _webAuthenticationService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ActivationApiService(
        IActivationService activationService,
        IWebAuthenticationService webAuthenticationService,
        IMapper mapper)
    {
        _activationService = activationService;
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
    public async Task<Envelope<User>> Activate(Guid token, ActivationRequest request, HttpContext httpContext)
    {
        // Map API DTO to service DTO
        var serviceRequest = new ServiceActivationRequest
        {
            Token = token,
            Username = request.Username,
            RetryEmail = request.RetryEmail
        };

        var userId = await _activationService.Activate(serviceRequest);

        // Auto-login: authenticate and set cookie
        var identity = await _webAuthenticationService.Authenticate(
            new UnconditionalCredentials { UserId = userId },
            httpContext);

        return new Envelope<User>(_mapper.Map<User>(identity.User));
    }

    /// <inheritdoc />
    public async Task ResendActivation(ResendActivation resendActivation)
    {
        await _activationService.ResendActivation(resendActivation.Email);
    }
}
