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
    private readonly ISymmetricCryptoService _cryptoService;
    private readonly ISecurityManager _securityManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISessionService _sessionService;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TwoFactorConfiguration _config;

    public TwoFactorService(
        ITwoFactorRepository repository,
        ITwoFactorVerifier verifier,
        ISymmetricCryptoService cryptoService,
        ISecurityManager securityManager,
        IIdentityProvider identityProvider,
        ISessionService sessionService,
        ISecurityAuditRepository auditRepository,
        IDateTimeProvider dateTimeProvider,
        IOptions<TwoFactorConfiguration> config)
    {
        _repository = repository;
        _verifier = verifier;
        _cryptoService = cryptoService;
        _securityManager = securityManager;
        _identityProvider = identityProvider;
        _sessionService = sessionService;
        _auditRepository = auditRepository;
        _dateTimeProvider = dateTimeProvider;
        _config = config.Value;
    }

    /// <inheritdoc />
    public async Task<TwoFactorStatus> GetStatus(CancellationToken cancellationToken = default)
    {
        var user = _identityProvider.RequireAuthenticated();
        var state = await _repository.Find(user.UserId, cancellationToken);
        var left = state is { ConfirmedUtc: not null }
            ? await _repository.CountUnusedRecoveryCodes(user.UserId, cancellationToken)
            : 0;

        return new TwoFactorStatus(
            Enabled: state?.IsConfirmed ?? false,
            EnabledUtc: state?.ConfirmedUtc,
            LastVerifiedUtc: state?.LastVerifiedUtc,
            RecoveryCodesLeft: left,
            RemovalDueUtc: state?.RemovalDueUtc);
    }

    /// <inheritdoc />
    public async Task<TwoFactorSetup> IssueSecret(
        string currentPassword, CancellationToken cancellationToken = default)
    {
        var user = _identityProvider.RequireAuthenticated();
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
        var base32 = TotpCalculator.ToBase32(secret);
        if (!await _repository.IssueSecret(
                user.UserId, await _cryptoService.Encrypt(base32), _dateTimeProvider.Now, cancellationToken))
        {
            // A confirmation committed after the read above: the same refusal.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorAlreadyEnabled);
        }

        return new TwoFactorSetup(base32, OtpAuthUri.For(_config.Issuer, user.Username, base32));
    }

    /// <inheritdoc />
    public async Task<RecoveryCodeSet> Confirm(
        string code, CancellationToken cancellationToken = default)
    {
        var user = _identityProvider.RequireAuthenticated();
        var state = await _repository.Find(user.UserId, cancellationToken);
        if (state == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TwoFactorSetupExpired);
        }

        if (state.IsConfirmed)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorAlreadyEnabled);
        }

        // The setup window, refused here before the sweep removes the secret.
        var window = TimeSpan.FromMinutes(_config.SetupWindowMinutes);
        if (state.CreatedUtc + window < _dateTimeProvider.Now)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.TwoFactorSetupExpired);
        }

        // Accepted, which also claims the time step: the code that switched the
        // factor on must not be usable a second later to pass a sign-in.
        if (!await _verifier.Accept(state, code, null, cancellationToken))
        {
            // In the journal under its own type, not in the login lockout counter:
            // a mistyped first code must not lock the owner out.
            await _auditRepository.LogAsync(user.UserId, SecurityEventType.TwoFactorSetupFailure);
            throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.TwoFactorRejected);
        }

        // Other sessions end before the factor goes on, so a failure here leaves
        // the account as it was and the confirmation can be retried.
        await _sessionService.LogoutElsewhere();

        // One transaction for the stamp and the set: the set is in this answer only.
        var (codes, hashes) = CreateRecoveryCodes();
        if (!await _repository.Confirm(
                user.UserId, state.CreatedUtc, _dateTimeProvider.Now, hashes, cancellationToken))
        {
            // The secret was replaced meanwhile, so the code no longer matches;
            // nobody guessed, so no journal entry.
            throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.TwoFactorRejected);
        }

        await _auditRepository.LogAsync(user.UserId, SecurityEventType.TwoFactorEnabled);
        return codes;
    }

    /// <inheritdoc />
    public async Task Disable(
        string currentPassword, string secondFactor, CancellationToken cancellationToken = default)
    {
        var user = _identityProvider.RequireAuthenticated();
        var state = await RequirePasswordAndFactor(
            user, currentPassword, secondFactor, cancellationToken);

        await _repository.Remove(state.UserId, cancellationToken);
        await _auditRepository.LogAsync(user.UserId, SecurityEventType.TwoFactorDisabled);
    }

    /// <inheritdoc />
    public async Task<RecoveryCodeSet> ReissueRecoveryCodes(
        string currentPassword, string secondFactor, CancellationToken cancellationToken = default)
    {
        var user = _identityProvider.RequireAuthenticated();
        await RequirePasswordAndFactor(user, currentPassword, secondFactor, cancellationToken);

        var (codes, hashes) = CreateRecoveryCodes();

        // The whole previous set goes at once.
        if (!await _repository.ReplaceRecoveryCodes(
                user.UserId, hashes, _dateTimeProvider.Now, cancellationToken))
        {
            // The factor went away since it was read: the write says so, not the codes.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.TwoFactorNotEnabled);
        }

        await _auditRepository.LogAsync(user.UserId, SecurityEventType.TwoFactorRecoveryCodesReissued);
        return codes;
    }

    /// <summary>
    /// A fresh set: the values for the one answer that carries them, and the
    /// hashes for the row that outlives it.
    /// </summary>
    private (RecoveryCodeSet Codes, IReadOnlyList<byte[]> Hashes) CreateRecoveryCodes()
    {
        var codes = RecoveryCodeFactory.Create(_config.RecoveryCodeCount);
        return (new RecoveryCodeSet(codes), codes.Select(RecoveryCodeFactory.Hash).ToList());
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
