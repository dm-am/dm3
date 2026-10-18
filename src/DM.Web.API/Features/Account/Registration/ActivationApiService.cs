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

    /// <inheritdoc />
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
        // Map API DTO to service DTO
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

        // The account is activated by the call above, and the session is the only
        // thing that can still be missing here. A passwordless session is refused
        // for an account that already carries a second factor, and a name chosen
        // seconds ago cannot carry one, so a guest at this line is a broken rule
        // rather than an answer to hand back. Unchecked, it returned a user with
        // no identity in it under a screen that said the account was ready, and
        // the reader was left signed out with nothing saying so.
        if (!identity.User.IsAuthenticated)
        {
            throw new InvalidOperationException(
                $"Activation of {userId} minted no session: {identity.Error}");
        }

        return new Envelope<User>(_mapper.ToUser(identity.User));
    }
}
