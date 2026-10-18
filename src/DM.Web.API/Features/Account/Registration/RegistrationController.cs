using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.Http;
using DM.Web.API.Shared.RateLimiting;
using System.Net;
using DM.Web.API.Swagger;
using DM.Domain.Core.Exceptions;
using System.Linq;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// User registration and activation (email-first flow)
/// </summary>
/// <remarks>
/// Registration flow:
/// 1. POST /v1/account/register - Submit email + password (creates pending registration)
/// 2. User receives activation email
/// 3. GET /v1/account/activation - Check token status and get email for UI
///    (token in the X-Dm-Account-Token header)
/// 4. POST /v1/account/activation - Complete activation with chosen username
///    (token in the X-Dm-Account-Token header)
///
/// For availability checks, use AvailabilityController:
/// - POST /v1/account/check-email - Check email availability
/// - POST /v1/account/check-username - Check username availability
///   (the value goes in the body: a query string is written to the access log)
/// </remarks>
[ApiController]
[Route("v1/account")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Registration")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class RegistrationController : ControllerBase
{
    private readonly IRegistrationApiService _registrationApiService;
    private readonly IActivationApiService _activationApiService;
    private readonly ILogger<RegistrationController> _logger;

    /// <inheritdoc />
    public RegistrationController(
        IRegistrationApiService registrationApiService,
        IActivationApiService activationApiService,
        ILogger<RegistrationController> logger)
    {
        _registrationApiService = registrationApiService;
        _activationApiService = activationApiService;
        _logger = logger;
    }

    /// <summary>
    /// Register new user (Step 1)
    /// </summary>
    /// <remarks>
    /// Creates a pending registration. An activation email will be sent to verify the address.
    /// User will choose their Login (username) after clicking the activation link (Step 2).
    ///
    /// If the email already has a pending registration, it will be replaced with new credentials.
    /// </remarks>
    /// <param name="registration">Email and password for registration</param>
    /// <response code="201">Pending registration created. Check email for activation link.</response>
    /// <response code="400">Validation error (invalid email format, password too short, etc.)</response>
    /// <response code="409">Email already registered (active user exists)</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPost("register", Name = nameof(Register))]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [CreatedWithoutLocation]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest registration)
    {
        // Honeypot check for bot protection
        if (!string.IsNullOrWhiteSpace(registration.Website))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["email"] = "Не удалось зарегистрироваться",
            }, RefusalMessage.InvalidData);
        }

        try
        {
            await _registrationApiService.Register(registration);
        }
        catch (ValidationException refusal) when (Discloses(refusal, "Email"))
        {
            // The same bit the availability check gives out, recorded the same
            // way: a taken address learned on submit is a disclosing answer of
            // the recorded table (SECURITY.md).
            _logger.IdentifierDisclosed(HttpContext, "register", ValidationError.Taken);
            throw;
        }

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Check activation token status
    /// </summary>
    /// <remarks>
    /// Returns token status and email for UI display.
    /// Use this before showing the Login selection form.
    ///
    /// Possible statuses:
    /// - "ready": Token is valid, show Login selection form
    /// - "expired": Token expired (>48h), offer to resend activation email
    /// </remarks>
    /// <param name="token">Activation token from the mailed link, in the X-Dm-Account-Token header</param>
    /// <response code="200">Token info (status and email)</response>
    /// <response code="404">Token missing, malformed or not found (already used or invalid)</response>
    [HttpGet("activation", Name = nameof(GetActivationInfo))]
    [ProducesResponseType(typeof(PendingInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActivationInfo(
        [FromHeader(Name = TokenHeaders.Account)] string? token)
    {
        var info = await _activationApiService.GetPendingInfo(TokenHeaders.ParseAccountToken(token));
        if (info == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrUsed);
        }

        return Ok(info);
    }

    /// <summary>
    /// Complete activation with Login selection (Step 2)
    /// </summary>
    /// <remarks>
    /// Creates user account with chosen Login (username).
    /// Auto-login via HttpOnly cookie on success.
    ///
    /// Login requirements:
    /// - 2-20 characters
    /// - No control characters, HTML/URL unsafe chars, quotes, brackets, or special symbols
    /// - No leading/trailing/consecutive whitespace
    /// - Must be unique (not used by active users or in login history)
    ///
    /// A click that lands twice is answered as success only while the pending
    /// link is still found and the write collides with this same account; a
    /// spent link answers 410 whatever the body says.
    /// </remarks>
    /// <param name="token">Activation token from the mailed link, in the X-Dm-Account-Token header</param>
    /// <param name="request">Chosen Login</param>
    /// <response code="200">User created and authenticated</response>
    /// <response code="400">Login validation failed (invalid format, already taken, etc.)</response>
    /// <response code="404">Token missing or malformed</response>
    /// <response code="409">Pending registration already used by another account</response>
    /// <response code="410">Token expired or not found</response>
    [HttpPost("activation", Name = nameof(Activate))]
    [ProducesResponseType(typeof(Envelope<DM.Web.API.Features.Community.Users.User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Activate(
        [FromHeader(Name = TokenHeaders.Account)] string? token,
        [FromBody] ActivationRequest request)
    {
        try
        {
            return Ok(await _activationApiService.Activate(TokenHeaders.ParseAccountToken(token), request, HttpContext));
        }
        catch (ValidationException refusal) when (Discloses(refusal, "Username"))
        {
            // Reached only by the holder of a live link: the service reads the
            // link before it looks at the name. Still a disclosing answer, and
            // still recorded (SECURITY.md).
            _logger.IdentifierDisclosed(HttpContext, "activation", ValidationError.Taken);
            throw;
        }
        catch (HttpBadRequestException refusal) when (Discloses(refusal, "Username"))
        {
            // The same answer by the other door: the name was free when the
            // validator looked and taken by the time the row was written.
            _logger.IdentifierDisclosed(HttpContext, "activation", ValidationError.Taken);
            throw;
        }
    }

    /// <summary>
    /// Whether a refused request told the caller that the field's value is
    /// somebody else's
    /// </summary>
    private static bool Discloses(ValidationException refusal, string field) =>
        refusal.Errors.Any(e => e.PropertyName == field && e.ErrorMessage == ValidationError.Taken);

    /// <summary>
    /// The same question for a refusal raised past the validator, by the write
    /// that lost the race for the value
    /// </summary>
    private static bool Discloses(HttpBadRequestException refusal, string field) =>
        refusal.ValidationErrors.TryGetValue(field, out var error) && error == ValidationError.Taken;
}
