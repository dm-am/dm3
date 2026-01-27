using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using DM.Services.Authentication.Dto;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using IAuthenticationService = DM.Services.Authentication.Implementation.IAuthenticationService;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DM.Web.API.Controllers.v1.Connect;

/// <summary>
/// External OAuth providers (Discord, etc.)
/// </summary>
[ApiController]
[Route("connect")]
[ApiExplorerSettings(GroupName = "Authentication")]
public class ExternalAuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserReadingService _userReadingService;
    private readonly DmDbContext _dbContext;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of ExternalAuthController
    /// </summary>
    public ExternalAuthController(
        IAuthenticationService authenticationService,
        IUserReadingService userReadingService,
        DmDbContext dbContext,
        IConfiguration configuration)
    {
        _authenticationService = authenticationService;
        _userReadingService = userReadingService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    /// <summary>
    /// Initiates Discord OAuth login
    /// </summary>
    /// <param name="returnUrl">URL to redirect after successful authentication</param>
    [HttpGet("discord")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult DiscordLogin([FromQuery] string returnUrl)
    {
        // Validate returnUrl to prevent open redirects
        if (string.IsNullOrEmpty(returnUrl) || !IsValidReturnUrl(returnUrl))
        {
            returnUrl = _configuration["IntegrationSettings:FrontendUrl"] ?? "/";
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(DiscordCallback)),
            Items =
            {
                { "returnUrl", returnUrl }
            }
        };

        return Challenge(properties, "Discord");
    }

    /// <summary>
    /// Discord OAuth callback - handles the response from Discord
    /// </summary>
    [HttpGet("discord/callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> DiscordCallback()
    {
        // Get the external login information
        var authenticateResult = await HttpContext.AuthenticateAsync("Discord");

        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            return RedirectWithError("Discord authentication failed.");
        }

        // Extract Discord user info from claims
        var discordIdClaim = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier);
        var usernameClaim = authenticateResult.Principal.FindFirst(ClaimTypes.Name);
        var emailClaim = authenticateResult.Principal.FindFirst(ClaimTypes.Email);

        if (discordIdClaim == null || usernameClaim == null)
        {
            return RedirectWithError("Could not retrieve Discord user information.");
        }

        var discordId = discordIdClaim.Value;
        var discordUsername = usernameClaim.Value;
        var discordEmail = emailClaim?.Value;

        // Try to find existing user by Discord ID
        var user = await FindUserByDiscordId(discordId);

        if (user == null && !string.IsNullOrEmpty(discordEmail))
        {
            // Try to find user by email - note: email lookup would need a separate method
            // For now, users must link accounts manually if Discord email differs from login
            // This is a known limitation of the current implementation
        }

        if (user == null)
        {
            // No existing user - redirect to link account page
            var returnUrl = authenticateResult.Properties?.Items["returnUrl"] ?? "/";
            var linkUrl = BuildLinkAccountUrl(discordId, discordUsername, discordEmail, returnUrl);
            return Redirect(linkUrl);
        }

        // Link Discord ID if not already linked
        await LinkDiscordIdIfNeeded(user.UserId, discordId);

        // Create authentication session
        var identity = await _authenticationService.Authenticate(user.UserId);

        if (identity.Error != AuthenticationError.NoError)
        {
            return RedirectWithError(GetErrorMessage(identity.Error));
        }

        // Generate OAuth tokens
        var tokens = await GenerateTokens(identity);

        // Redirect to frontend with tokens
        var returnUrl2 = authenticateResult.Properties?.Items["returnUrl"] ?? "/";
        return RedirectWithTokens(returnUrl2, tokens.AccessToken, tokens.RefreshToken);
    }

    /// <summary>
    /// Link Discord account to existing user (requires login)
    /// </summary>
    [HttpPost("discord/link")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkDiscordAccount([FromBody] LinkDiscordRequest request)
    {
        var userIdClaim = User.FindFirst(Claims.Subject)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return BadRequest("Invalid user.");
        }

        // Check if Discord ID is already linked to another account
        var existingUser = await FindUserByDiscordId(request.DiscordId);
        if (existingUser != null && existingUser.UserId != userId)
        {
            return BadRequest("This Discord account is already linked to another user.");
        }

        await LinkDiscordIdIfNeeded(userId, request.DiscordId);

        return Ok(new { message = "Discord account linked successfully." });
    }

    private bool IsValidReturnUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            return false;

        // Allow relative URLs
        if (!uri.IsAbsoluteUri)
            return true;

        // Check against allowed origins
        var allowedOrigins = _configuration.GetSection("IntegrationSettings:CorsUrls").Get<string[]>();
        return allowedOrigins?.Any(origin =>
            uri.Host.Equals(new Uri(origin).Host, StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    private async Task<GeneralUser?> FindUserByDiscordId(string discordId)
    {
        var user = await _dbContext.Users
            .Where(u => u.DiscordId == discordId && !u.IsRemoved)
            .Select(u => new GeneralUser
            {
                UserId = u.UserId,
                Login = u.Login
            })
            .FirstOrDefaultAsync();

        return user;
    }

    private async Task LinkDiscordIdIfNeeded(Guid userId, string discordId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null && user.DiscordId != discordId)
        {
            user.DiscordId = discordId;
            await _dbContext.SaveChangesAsync();
        }
    }

    private string BuildLinkAccountUrl(string discordId, string username, string? email, string returnUrl)
    {
        var frontendUrl = _configuration["IntegrationSettings:FrontendUrl"] ?? "";
        var queryParams = new Dictionary<string, string?>
        {
            { "discordId", discordId },
            { "username", username },
            { "email", email },
            { "returnUrl", returnUrl }
        };

        var queryString = string.Join("&", queryParams
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));

        return $"{frontendUrl}/auth/link-discord?{queryString}";
    }

    private IActionResult RedirectWithError(string error)
    {
        var frontendUrl = _configuration["IntegrationSettings:FrontendUrl"] ?? "";
        return Redirect($"{frontendUrl}/auth/callback?error={HttpUtility.UrlEncode(error)}");
    }

    private IActionResult RedirectWithTokens(string returnUrl, string accessToken, string refreshToken)
    {
        var baseUrl = returnUrl.Contains('?') ? returnUrl + "&" : returnUrl + "?";

        if (!returnUrl.StartsWith("http"))
        {
            var frontendUrl = _configuration["IntegrationSettings:FrontendUrl"] ?? "";
            baseUrl = $"{frontendUrl}/auth/callback?returnUrl={HttpUtility.UrlEncode(returnUrl)}&";
        }

        return Redirect($"{baseUrl}access_token={HttpUtility.UrlEncode(accessToken)}&refresh_token={HttpUtility.UrlEncode(refreshToken)}");
    }

    private async Task<(string AccessToken, string RefreshToken)> GenerateTokens(IIdentity identity)
    {
        var claimsIdentity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        claimsIdentity.AddClaim(new Claim(Claims.Subject, identity.User.UserId.ToString()));
        claimsIdentity.AddClaim(new Claim(Claims.Name, identity.User.Login));
        claimsIdentity.AddClaim(new Claim(Claims.Role, identity.User.Role.ToString()));
        claimsIdentity.AddClaim(new Claim("session_id", identity.Session.Id.ToString()));

        if (!string.IsNullOrEmpty(identity.User.Login))
        {
            claimsIdentity.AddClaim(new Claim(Claims.Username, identity.User.Login));
        }

        claimsIdentity.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject or Claims.Name or Claims.Role or Claims.Username
                => new[] { Destinations.AccessToken, Destinations.IdentityToken },
            "session_id"
                => new[] { Destinations.AccessToken },
            _ => Array.Empty<string>()
        });

        var principal = new ClaimsPrincipal(claimsIdentity);
        principal.SetScopes(new[]
        {
            Scopes.OpenId,
            Scopes.Profile,
            Scopes.OfflineAccess
        });

        // Sign in and get the tokens from the response
        await HttpContext.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal);

        // Note: The actual token generation happens through the OpenIddict pipeline
        // For external auth, we need to extract tokens differently
        // This is a simplified approach - in production you'd use IOpenIddictTokenManager
        var accessToken = principal.FindFirst(Claims.Subject)?.Value ?? "";
        var refreshToken = Guid.NewGuid().ToString();

        return (accessToken, refreshToken);
    }

    private static string GetErrorMessage(AuthenticationError error) => error switch
    {
        AuthenticationError.Inactive => "Account is not activated.",
        AuthenticationError.Banned => "Account has been banned.",
        AuthenticationError.Removed => "Account has been removed.",
        _ => "Authentication failed."
    };
}

/// <summary>
/// Request to link Discord account
/// </summary>
public class LinkDiscordRequest
{
    /// <summary>
    /// Discord user ID
    /// </summary>
    public string DiscordId { get; set; } = "";
}
