using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using Microsoft.Extensions.Logging;
using ReservedName = DM.Domain.Core.Users.RemovedAccount;

namespace DM.Domain.Account.Features.Deactivation;

/// <inheritdoc />
internal class DeactivationService : IDeactivationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserLookupService _userLookupService;
    private readonly IDeactivationRepository _repository;
    private readonly IAccountRemover _remover;
    private readonly IAccountRemovalMailSender _removalMailSender;
    private readonly IAccountRestorationMailSender _restorationMailSender;
    private readonly ILogger<DeactivationService> _logger;

    public DeactivationService(
        IIdentityProvider identityProvider,
        IUserLookupService userLookupService,
        IDeactivationRepository repository,
        IAccountRemover remover,
        IAccountRemovalMailSender removalMailSender,
        IAccountRestorationMailSender restorationMailSender,
        ILogger<DeactivationService> logger)
    {
        _identityProvider = identityProvider;
        _userLookupService = userLookupService;
        _repository = repository;
        _remover = remover;
        _removalMailSender = removalMailSender;
        _restorationMailSender = restorationMailSender;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DeactivateByModeration(string username, string reason,
        CancellationToken cancellationToken = default)
    {
        var currentUser = RequireModeration();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["reason"] = ValidationError.Empty
            });
        }

        var targetUser = await _userLookupService.GetAsync(username);

        // The reach of a ban (ModerationRank): there is deliberately no in-app
        // authority over a site owner, and a rank cannot be turned against an
        // equal rank - two senior moderators would otherwise remove each other,
        // and this one is not a two-day window that somebody can undo.
        if (!ModerationRank.Reaches(currentUser.Role, targetUser.Role))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        await _remover.Remove(targetUser.UserId, cancellationToken);

        // The reason lives in the log and nowhere else: the owner is told the
        // account is removed only after a proven password, and even then not
        // why - the way back runs through support, where a person reads this.
        //
        // Written before the letter leaves, because the record of who decided
        // what must not depend on a mail transport being up.
        _logger.LogWarning(
            "Account removed by moderation. UserId={UserId}, ModeratorId={ModeratorId}, Reason={Reason}",
            targetUser.UserId, currentUser.UserId, reason);

        await Notify(targetUser.Email, address => _removalMailSender.SendCompleted(address));
    }

    /// <inheritdoc />
    public async Task RestoreByModeration(string username, CancellationToken cancellationToken = default)
    {
        var currentUser = RequireModeration();

        // Past the global filter, which is the only reason this read exists: to
        // every other lookup in the product a removed account does not exist, so
        // the name would come back unknown and there would be nothing to restore.
        var account = await _repository.GetRemovedByUsername(username, cancellationToken)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));

        // The name is checked on the row, not on what was asked: the lookup compares
        // names without case, and it is the stored spelling that would come back.
        // "Аккаунт удален" and its lookalikes are the signature of every removed
        // account (the DM2 shared account is imported as one of them), and a live
        // row under that name would take the signature and the profile address.
        if (ReservedName.IsReserved(account.Username))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.ReservedNameNotRestorable);
        }

        await _repository.Restore(account.UserId, cancellationToken);

        _logger.LogWarning(
            "Account restored by moderation. UserId={UserId}, ModeratorId={ModeratorId}",
            account.UserId, currentUser.UserId);

        await Notify(account.Email, address => _restorationMailSender.SendRestored(address));
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<RemovedAccount> Accounts, PagingResult Paging)> GetRemovedAccounts(
        RemovedAccountsQuery query, CancellationToken cancellationToken = default)
    {
        var currentUser = RequireModeration();

        var totalCount = await _repository.CountRemoved(query.Search, cancellationToken);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);
        var accounts = await _repository.GetRemoved(query.Search, paging, cancellationToken);

        // The address is an administrator's to read: a moderated profile shows it
        // to an administrator and to nobody else, and a list that ignored that
        // would hand every address on the page to a moderator who may not see one.
        // Matching a search against the column is a different act - the term is
        // one the sender of a support letter already quoted.
        if (currentUser.Role < UserRole.Admin)
        {
            accounts = accounts.Select(account => account with { Email = null }).ToList();
        }

        return (accounts, paging.Result);
    }

    /// <summary>
    /// The caller, once he is allowed to remove and restore accounts at all.
    /// </summary>
    private AuthenticatedUser RequireModeration()
    {
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role < UserRole.SeniorModerator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        return currentUser;
    }

    /// <summary>
    /// Send the letter the decision earns, when there is an address to send it to.
    /// </summary>
    /// <remarks>
    /// An account always has an address, which is why a missing one is worth a
    /// line in the log rather than a refusal: the decision has already been taken
    /// and written down, and failing the request now would only hide it.
    /// </remarks>
    private Task Notify(string? email, Func<string, Task> send)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            return send(email);
        }

        _logger.LogWarning("Account has no address to send the moderation letter to");
        return Task.CompletedTask;
    }
}
