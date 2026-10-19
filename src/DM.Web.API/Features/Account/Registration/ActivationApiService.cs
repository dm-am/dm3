using System;
using System.Threading.Tasks;
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
    private readonly UserMapper _mapper;

    public ActivationApiService(
        IActivationService activationService,
        IWebAuthenticationService webAuthenticationService,
        UserMapper mapper)
    {
        _activationService = activationService;
        _webAuthenticationService = webAuthenticationService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PendingInfoResponse?> GetPendingInfo(Guid token)
    {
        var result = await _activationService.GetPendingInfo(token);

        if (result == null)
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
        var serviceRequest = new ServiceActivationRequest
        {
            Token = token,
            Username = request.Username,
            TimeZoneId = request.TimeZoneId
        };

        var userId = await _activationService.Activate(serviceRequest);

        // Auto-login: authenticate and set cookie
        var identity = await _webAuthenticationService.Authenticate(
            new UnconditionalCredentials { UserId = userId },
            httpContext);

        // A new account carries no second factor, so a guest here is a broken rule,
        // not an answer: the screen would say "ready" to a reader left signed out.
        if (!identity.User.IsAuthenticated)
        {
            throw new InvalidOperationException(
                $"Activation of {userId} minted no session: {identity.Error}");
        }

        return new Envelope<User>(_mapper.ToUser(identity.User));
    }
}
