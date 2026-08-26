using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.TwoFactor;

/// <inheritdoc />
internal class TwoFactorService : ITwoFactorService
{
    /// <summary>
    /// Bytes of secret. RFC 4226 names 160 bits as the recommended length for
    /// HMAC-SHA1, and the secret becomes Base32 only at the moment it is handed
    /// over.
    /// </summary>
    private const int SecretBytes = 20;

    private readonly ITwoFactorRepository _repository;
    private readonly ITwoFactorVerifier _verifier;
    private readonly ITotpCalculator _calculator;
    private readonly IRecoveryCodeFactory _recoveryCodes;
    private readonly ISymmetricCryptoService _cryptoService;
    private readonly ISecurityManager _securityManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IAuthenticationService _authenticationService;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TwoFactorConfiguration _config;

    public TwoFactorService(
        ITwoFactorRepository repository,
        ITwoFactorVerifier verifier,
        ITotpCalculator calculator,
        IRecoveryCodeFactory recoveryCodes,
        ISymmetricCryptoService cryptoService,
        ISecurityManager securityManager,
        IIdentityProvider identityProvider,
        IAuthenticationService authenticationService,
        ISecurityAuditRepository auditService,
        IDateTimeProvider dateTimeProvider,
        IOptions<TwoFactorConfiguration> config)
    {
        _repository = repository;
        _verifier = verifier;
        _calculator = calculator;
        _recoveryCodes = recoveryCodes;
        _cryptoService = cryptoService;
        _securityManager = securityManager;
        _identityProvider = identityProvider;
        _authenticationService = authenticationService;
        _auditService = auditService;
        _dateTimeProvider = dateTimeProvider;
        _config = config.Value;
    }

    /// <inheritdoc />
    public async Task<TwoFactorStatus> GetStatus(CancellationToken cancellationToken = default)
    {
        var user = Owner();
        var state = await _repository.Find(user.UserId, cancellationToken);
        var left = state is { ConfirmedUtc: not null }
            ? await _repository.CountUnusedRecoveryCodes(user.UserId, cancellationToken)
            : 0;

        return new TwoFactorStatus(
            Enabled: state?.IsConfirmed ?? false,
            EnabledUtc: state?.ConfirmedUtc,
            LastVerifiedUtc: state?.LastVerifiedUtc,
            RecoveryCodesLeft: left,
            RemovalDueUtc: state?.RemovalDueUtc,
            Required: TwoFactorRequirement.AppliesTo(user.RecordedRole),
            PrivilegeWithheld: user.PrivilegeWithheld);
    }

    /// <inheritdoc />
    public async Task<TwoFactorSetup> IssueSecret(
        string currentPassword, CancellationToken cancellationToken = default)
    {
        var user = Owner();
        RequirePassword(user, currentPassword);

        var existing = await _repository.Find(user.UserId, cancellationToken);
        if (existing is { ConfirmedUtc: not null })
        {
            // Switching on what is already on would mean two live secrets, and
            // the owner could not tell which device the account now answers to.
            // Replacing a device is off and on again.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorAlreadyEnabled);
        }

        var secret = RandomNumberGenerator.GetBytes(SecretBytes);
        var base32 = _calculator.ToBase32(secret);
        await _repository.IssueSecret(
            user.UserId, await _cryptoService.Encrypt(base32), _dateTimeProvider.Now, cancellationToken);

        return new TwoFactorSetup(base32, OtpAuthUri.For(_config.Issuer, user.Username, base32));
    }

    /// <inheritdoc />
    public async Task<RecoveryCodeSet> Confirm(
        string code, CancellationToken cancellationToken = default)
    {
        var user = Owner();
        var state = await _repository.Find(user.UserId, cancellationToken);
        if (state == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TwoFactorSetupExpired);
        }

        if (state.IsConfirmed)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorAlreadyEnabled);
        }

        // The setup window. A secret nobody confirmed is a credential lying in
        // the table belonging to somebody who has forgotten about it, and the
        // sweep takes it - but the refusal has to stand before the sweep runs,
        // or the window would be "thirty minutes, give or take an hour".
        var window = TimeSpan.FromMinutes(_config.SetupWindowMinutes);
        if (state.CreatedUtc + window < _dateTimeProvider.Now)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.TwoFactorSetupExpired);
        }

        // Accepted, which also claims the time step: the code that switched the
        // factor on must not be usable a second later to pass a sign-in.
        if (!await _verifier.Accept(state, code, null, cancellationToken))
        {
            // Counted, and counted in the journal rather than in the lockout
            // counter of the login. A person who mistypes the very first code
            // while setting the factor up must not be able to lock their own
            // sign-in doing it; what stops guessing here is the rate limit on
            // the endpoint, and what the owner needs is a record that somebody
            // was trying. Under its own type and not the login one: there was a
            // session, no password was asked for and nothing was signed into.
            await _auditService.LogAsync(user.UserId, SecurityEventType.TwoFactorSetupFailure);
            throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.TwoFactorRejected);
        }

        // Before the factor goes on, not after. Switching it on while somebody
        // else's year-long session sits inside the account is locking a door with
        // the burglar behind it (INV-16), and a failure here after the stamp
        // would leave exactly that state with nothing left to retry: the factor
        // would already be on and the confirmation would refuse a second run.
        // Failing first costs the owner a re-confirmation and leaves the account
        // as it was.
        await _authenticationService.LogoutElsewhere();

        // One transaction for the stamp and the set. The set is in this answer
        // and in no other, so a factor switched on beside a set that never
        // stored would tie the owner to one device with nothing to get past it.
        var (codes, hashes) = CreateRecoveryCodes();
        if (!await _repository.Confirm(user.UserId, _dateTimeProvider.Now, hashes, cancellationToken))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorAlreadyEnabled);
        }

        await _auditService.LogAsync(user.UserId, SecurityEventType.TwoFactorEnabled);
        return codes;
    }

    /// <inheritdoc />
    public async Task Disable(
        string currentPassword, string secondFactor, CancellationToken cancellationToken = default)
    {
        var user = Owner();
        var state = await RequirePasswordAndFactor(
            user, currentPassword, secondFactor, cancellationToken);

        await _repository.Remove(state.UserId, cancellationToken);
        await _auditService.LogAsync(user.UserId, SecurityEventType.TwoFactorDisabled);
    }

    /// <inheritdoc />
    public async Task<RecoveryCodeSet> ReissueRecoveryCodes(
        string currentPassword, string secondFactor, CancellationToken cancellationToken = default)
    {
        var user = Owner();
        await RequirePasswordAndFactor(user, currentPassword, secondFactor, cancellationToken);

        var (codes, hashes) = CreateRecoveryCodes();

        // The whole previous set goes at once. A set that is half old and half
        // new means a code the owner crossed off on paper still opens the
        // account.
        await _repository.ReplaceRecoveryCodes(
            user.UserId, hashes, _dateTimeProvider.Now, cancellationToken);

        await _auditService.LogAsync(user.UserId, SecurityEventType.TwoFactorRecoveryCodesReissued);
        return codes;
    }

    /// <summary>
    /// A fresh set: the values for the one answer that carries them, and the
    /// hashes for the row that outlives it.
    /// </summary>
    private (RecoveryCodeSet Codes, IReadOnlyList<byte[]> Hashes) CreateRecoveryCodes()
    {
        var codes = _recoveryCodes.Create(_config.RecoveryCodeCount);
        return (new RecoveryCodeSet(codes), codes.Select(_recoveryCodes.Hash).ToList());
    }

    private AuthenticatedUser Owner()
    {
        var identity = _identityProvider.Current;
        if (!identity.User.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
        }

        return identity.User;
    }

    private void RequirePassword(AuthenticatedUser user, string password)
    {
        if (!_securityManager.ComparePasswords(password, user.Salt, user.PasswordHash))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["password"] = RefusalMessage.WrongPassword
            });
        }
    }

    private async Task<TwoFactorState> RequirePasswordAndFactor(
        AuthenticatedUser user, string password, string secondFactor,
        CancellationToken cancellationToken)
    {
        RequirePassword(user, password);

        var state = await _repository.Find(user.UserId, cancellationToken);
        if (state is not { ConfirmedUtc: not null })
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        if (!await _verifier.Accept(state, secondFactor, null, cancellationToken))
        {
            throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.TwoFactorRejected);
        }

        return state;
    }
}
