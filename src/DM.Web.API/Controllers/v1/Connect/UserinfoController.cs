using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DM.Web.API.Controllers.v1.Connect;

/// <summary>
/// OAuth 2.0 Userinfo endpoint controller
/// </summary>
[ApiController]
[Route("connect")]
[ApiExplorerSettings(GroupName = "Authentication")]
public class UserinfoController : ControllerBase
{
    /// <summary>
    /// OAuth 2.0 Userinfo endpoint - returns claims about the authenticated user
    /// </summary>
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("userinfo")]
    [HttpPost("userinfo")]
    [Produces("application/json")]
    public IActionResult Userinfo()
    {
        // Get the authenticated user
        var user = User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The specified access token is not valid."
                }));
        }

        // Build the claims dictionary
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            // The "sub" claim is required and represents the subject (user identifier)
            [Claims.Subject] = user.FindFirst(Claims.Subject)?.Value ??
                              user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        };

        // Add optional standard claims if available
        if (user.HasClaim(c => c.Type == Claims.Name))
        {
            claims[Claims.Name] = user.FindFirst(Claims.Name).Value;
        }

        if (user.HasClaim(c => c.Type == Claims.Username))
        {
            claims[Claims.Username] = user.FindFirst(Claims.Username).Value;
        }

        if (user.HasClaim(c => c.Type == Claims.Role))
        {
            claims[Claims.Role] = user.FindFirst(Claims.Role).Value;
        }

        if (user.HasClaim(c => c.Type == Claims.Email))
        {
            claims[Claims.Email] = user.FindFirst(Claims.Email).Value;
        }

        // Add custom claims if available
        if (user.HasClaim(c => c.Type == "session_id"))
        {
            claims["session_id"] = user.FindFirst("session_id").Value;
        }

        return Ok(claims);
    }
}
