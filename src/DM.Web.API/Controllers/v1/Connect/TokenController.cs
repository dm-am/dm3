using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Core.Dto.Enums;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DM.Web.API.Controllers.v1.Connect;

/// <summary>
/// OAuth 2.0 Token endpoint controller
/// </summary>
[ApiController]
[Route("connect")]
[ApiExplorerSettings(GroupName = "Authentication")]
[EnableRateLimiting("auth")]
public class TokenController : ControllerBase
{
    private readonly DM.Services.Authentication.Implementation.IAuthenticationService _dmAuthenticationService;

    /// <summary>
    /// Initializes a new instance of the TokenController
    /// </summary>
    public TokenController(DM.Services.Authentication.Implementation.IAuthenticationService dmAuthenticationService)
    {
        _dmAuthenticationService = dmAuthenticationService;
    }

    /// <summary>
    /// OAuth 2.0 Token endpoint - handles password and refresh_token grants
    /// </summary>
    [HttpPost("token")]
    [Produces("application/json")]
    [AllowAnonymous]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordGrant(request);
        }

        if (request.IsRefreshTokenGrantType())
        {
            return await HandleRefreshTokenGrant(request);
        }

        return BadRequest(new OpenIddictResponse
        {
            Error = Errors.UnsupportedGrantType,
            ErrorDescription = "The specified grant type is not supported."
        });
    }

    private async Task<IActionResult> HandlePasswordGrant(OpenIddictRequest request)
    {
        // Validate credentials using existing authentication service
        var identity = await _dmAuthenticationService.Authenticate(
            request.Username,
            request.Password,
            persistent: false); // OAuth tokens handle their own persistence

        if (identity.Error != AuthenticationError.NoError)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = GetErrorCode(identity.Error),
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = GetErrorDescription(identity.Error)
                }));
        }

        // Create claims principal
        var principal = CreateClaimsPrincipal(identity);

        // Set the list of scopes granted to the client application
        principal.SetScopes(new[]
        {
            Scopes.OpenId,
            Scopes.Profile,
            Scopes.OfflineAccess
        });

        // Automatically create the access token and refresh token
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenGrant(OpenIddictRequest request)
    {
        // Retrieve the claims principal stored in the refresh token
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The refresh token is no longer valid."
                }));
        }

        var userIdClaim = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The refresh token is invalid."
                }));
        }

        // Validate that the user still exists and is active
        var identity = await _dmAuthenticationService.Authenticate(userId);

        if (identity.Error != AuthenticationError.NoError)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The refresh token is no longer valid."
                }));
        }

        // Create a new claims principal with updated information
        var principal = CreateClaimsPrincipal(identity);

        // Restore the scopes from the original token
        principal.SetScopes(result.Principal.GetScopes());

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(IIdentity identity)
    {
        var claimsIdentity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        // Add standard claims
        claimsIdentity.AddClaim(new Claim(Claims.Subject, identity.User.UserId.ToString()));
        claimsIdentity.AddClaim(new Claim(Claims.Name, identity.User.Login));
        claimsIdentity.AddClaim(new Claim(Claims.Role, identity.User.Role.ToString()));

        // Add custom claims
        claimsIdentity.AddClaim(new Claim("session_id", identity.Session.Id.ToString()));

        // Add email if available
        if (!string.IsNullOrEmpty(identity.User.Login))
        {
            claimsIdentity.AddClaim(new Claim(Claims.Username, identity.User.Login));
        }

        // Set destinations for claims (which tokens they appear in)
        claimsIdentity.SetDestinations(claim => claim.Type switch
        {
            // Include subject in both access and identity tokens
            Claims.Subject or Claims.Name or Claims.Role or Claims.Username
                => new[] { Destinations.AccessToken, Destinations.IdentityToken },

            // Include session_id only in access tokens
            "session_id"
                => new[] { Destinations.AccessToken },

            // Never include other claims
            _ => Array.Empty<string>()
        });

        return new ClaimsPrincipal(claimsIdentity);
    }

    private static string GetErrorCode(AuthenticationError error)
    {
        return error switch
        {
            AuthenticationError.WrongLogin or AuthenticationError.WrongPassword => Errors.InvalidGrant,
            AuthenticationError.Inactive => Errors.AccessDenied,
            AuthenticationError.Banned or AuthenticationError.Removed => Errors.AccessDenied,
            AuthenticationError.SessionExpired => Errors.InvalidGrant,
            AuthenticationError.ForgedToken => Errors.InvalidGrant,
            _ => Errors.ServerError
        };
    }

    private static string GetErrorDescription(AuthenticationError error)
    {
        return error switch
        {
            AuthenticationError.WrongLogin => "Invalid username or password.",
            AuthenticationError.WrongPassword => "Invalid username or password.",
            AuthenticationError.Inactive => "The user account is not activated.",
            AuthenticationError.Banned => "The user account has been banned.",
            AuthenticationError.Removed => "The user account has been removed.",
            AuthenticationError.SessionExpired => "The session has expired.",
            AuthenticationError.ForgedToken => "The token is invalid.",
            _ => "An error occurred while processing the authentication request."
        };
    }
}
