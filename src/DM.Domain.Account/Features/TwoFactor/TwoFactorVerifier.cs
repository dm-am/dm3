using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.TwoFactor;

/// <inheritdoc />
internal class TwoFactorVerifier : ITwoFactorVerifier
{
    private readonly ITwoFactorRepository _repository;
    private readonly ITotpCalculator _calculator;
    private readonly IRecoveryCodeFactory _recoveryCodes;
    private readonly ISymmetricCryptoService _cryptoService;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TwoFactorConfiguration _config;

    public TwoFactorVerifier(
        ITwoFactorRepository repository,
        ITotpCalculator calculator,
        IRecoveryCodeFactory recoveryCodes,
        ISymmetricCryptoService cryptoService,
        ISecurityAuditRepository auditService,
        IDateTimeProvider dateTimeProvider,
        IOptions<TwoFactorConfiguration> config)
    {
        _repository = repository;
        _calculator = calculator;
        _recoveryCodes = recoveryCodes;
        _cryptoService = cryptoService;
        _auditService = auditService;
        _dateTimeProvider = dateTimeProvider;
        _config = config.Value;
    }

    /// <inheritdoc />
    public async Task<bool> Accept(
        TwoFactorState state, string value, string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        // Six digits is a code from the device; anything else is offered to the
        // recovery set. The shape decides which store is asked, never which
        // answer is given: both refusals are the same refusal.
        return IsDeviceCode(trimmed)
            ? await AcceptDeviceCode(state, trimmed, cancellationToken)
            : await AcceptRecoveryCode(state, trimmed, ipAddress, cancellationToken);
    }

    private static bool IsDeviceCode(string value) =>
        value.Length == TotpCalculator.Digits && value.All(char.IsAsciiDigit);

    private async Task<bool> AcceptDeviceCode(
        TwoFactorState state, string code, CancellationToken cancellationToken)
    {
        var secret = await DecryptSecret(state);

        var matchedStep = _calculator.Match(
            secret, code, _dateTimeProvider.Now, _config.VerificationWindowSteps);
        if (matchedStep == null)
        {
            return false;
        }

        // The replay guard, and the reason the step number is asked for at all.
        // A code seen over a shoulder or lifted by a proxy is arithmetically
        // valid for up to ninety seconds; a step already recorded is refused
        // whatever the arithmetic says. Conditional in storage, so two requests
        // arriving with one code cannot both win.
        return await _repository.TryAcceptStep(
            state.UserId, matchedStep.Value, _dateTimeProvider.Now, cancellationToken);
    }

    private async Task<byte[]> DecryptSecret(TwoFactorState state) =>
        _calculator.FromBase32(await _cryptoService.Decrypt(state.Secret));

    private async Task<bool> AcceptRecoveryCode(
        TwoFactorState state, string value, string? ipAddress, CancellationToken cancellationToken)
    {
        var presented = _recoveryCodes.Hash(value);
        var stored = await _repository.GetUnusedRecoveryCodes(state.UserId, cancellationToken);

        // Every candidate is compared, and each comparison is fixed-time: a loop
        // that stopped at the first match would answer a near miss faster than a
        // miss, and the whole set is ten rows.
        Guid? matched = null;
        foreach (var (recoveryCodeId, codeHash) in stored)
        {
            if (CryptographicOperations.FixedTimeEquals(presented, codeHash))
            {
                matched = recoveryCodeId;
            }
        }

        if (matched == null)
        {
            return false;
        }

        // Conditional on the code still being unspent: this is what makes one
        // code good for exactly one login, however many requests carry it.
        var spent = await _repository.TrySpendRecoveryCode(
            matched.Value, _dateTimeProvider.Now, ipAddress, cancellationToken);
        if (!spent)
        {
            return false;
        }

        await _repository.MarkVerified(state.UserId, _dateTimeProvider.Now, cancellationToken);

        // Counted after the spend, so the number in the entry is what is left
        // and not what was left. The remainder belongs in the entry rather than
        // in a separate request for the state: the owner reads this line to find
        // out how close he is to having nothing left, and a line that only says
        // a code was used sends him elsewhere for the half of the fact that
        // matters.
        var left = await _repository.CountUnusedRecoveryCodes(state.UserId, cancellationToken);

        // Written here rather than in each of the three callers: spending a
        // recovery code is the same event whichever door it came through, and
        // the owner reading the journal is asking whether somebody used one of
        // his paper codes.
        await _auditService.LogAsync(
            state.UserId, SecurityEventType.TwoFactorRecoveryCodeUsed, ipAddress,
            details: $"Осталось резервных кодов: {left}");
        return true;
    }
}
