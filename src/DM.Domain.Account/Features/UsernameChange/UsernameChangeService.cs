using System;
using System.Text.RegularExpressions;
using DM.Domain.Core.Abstractions;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;
using DM.Domain.Core.Users;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal partial class UsernameChangeService : IUsernameChangeService
{
    // Forbidden: control chars, HTML/URL unsafe, quotes, brackets, special chars, zero-width
    // Whitespace: not at start/end, not consecutive
    // See: docs/conventions/USERNAME_POLICY.md
    [GeneratedRegex(UsernamePolicy.Pattern)]
    private static partial Regex UsernameValidationRegex();

    private readonly IValidator<CreateUsernameChangeRequest> _validator;
    private readonly IUsernameChangeRepository _repository;
    private readonly IUsernameHistoryRepository _historyRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUsernameChangeMailSender _notificationSender;

    public UsernameChangeService(
        IValidator<CreateUsernameChangeRequest> validator,
        IUsernameChangeRepository repository,
        IUsernameHistoryRepository historyRepository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IUsernameChangeMailSender notificationSender)
    {
        _validator = validator;
        _repository = repository;
        _historyRepository = historyRepository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _notificationSender = notificationSender;
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry> CreateAsync(CreateUsernameChangeRequest request)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);

        await _validator.ValidateAndThrowAsync(request);

        // A request awaiting a moderator and an approval whose name is not yet
        // chosen are both changes already in flight. A finished one - rejected,
        // completed, or expired by either deadline - holds nothing, and asking
        // again is the only way forward from it.
        var existing = await _repository.GetActiveByUserId(currentUser.UserId, _dateTimeProvider.Now);
        if (existing != null)
            throw new HttpException(HttpStatusCode.Conflict,
                existing.Status == UsernameChangeRequestStatus.Approved
                    ? RefusalMessage.UsernameChangeAlreadyApproved
                    : RefusalMessage.UsernameChangeAlreadyFiled);

        var now = _dateTimeProvider.Now;
        var dto = new UsernameChangeRequest
        {
            RequestId = _guidFactory.Create(),
            UserId = currentUser.UserId,
            // Username is NOT set here - user chooses it after approval
            RequestedUsername = null,
            Reason = request.Reason,
            Status = UsernameChangeRequestStatus.Pending,
            CreatedUtc = now
        };

        try
        {
            await _repository.Add(dto);
        }
        catch (DuplicateEntityException)
        {
            // Two requests sent at once: the index refused the second, and the
            // answer is the same one the pre-check would have given.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.UsernameChangeAlreadyFiled);
        }

        return new UsernameChangeRequestEntry
        {
            RequestId = dto.RequestId,
            CurrentUsername = currentUser.Username,
            UserId = currentUser.UserId,
            RequestedUsername = null,
            Reason = dto.Reason,
            Status = dto.Status,
            CreatedUtc = dto.CreatedUtc
        };
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry?> GetCurrentUserRequestAsync()
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);

        var entity = await _repository.GetLatestByUserId(currentUser.UserId);
        if (entity == null) return null;

        return MapToEntry(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<UsernameChangeRequestEntry>> GetPendingRequestsAsync()
    {
        return await _repository.GetPendingRequests();
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry> GetByIdAsync(Guid requestId)
    {
        var entity = await _repository.GetById(requestId);
        if (entity == null)
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UsernameChangeRequestNotFound);

        return MapToEntry(entity);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry> ResolveAsync(ResolveUsernameChangeRequest resolve)
    {
        var currentUser = _identityProvider.Current.User;
        var request = await _repository.GetById(resolve.RequestId);
        if (request == null)
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UsernameChangeRequestNotFound);

        if (request.Status != UsernameChangeRequestStatus.Pending)
            throw new HttpException(HttpStatusCode.Conflict, "Заявка уже рассмотрена");

        // Resolving is approving or rejecting and nothing else. The status arrives
        // in the body and used to be written through unchecked, so a request could
        // be filed as Completed or Expired: it left the queue looking finished
        // while no name changed and no approval link went out. Those two statuses
        // are reached by the flow itself - one by the user finishing the change,
        // one by the expiry job - and are not a moderator's to assert.
        if (resolve.Status != UsernameChangeRequestStatus.Approved &&
            resolve.Status != UsernameChangeRequestStatus.Rejected)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(resolve.Status)] = "Заявку можно только одобрить или отклонить"
            });
        }

        var now = _dateTimeProvider.Now;

        if (resolve.Status == UsernameChangeRequestStatus.Approved)
        {
            // Generate approval token (user will use this to complete the change)
            request.ApprovalToken = _guidFactory.Create();
            request.ApprovalTokenExpiresUtc = now.AddHours(48); // Token valid for 48 hours
        }

        request.Status = resolve.Status;
        request.ResolvedUtc = now;
        request.ResolvedByUserId = currentUser.UserId;
        request.ResolverComment = resolve.Comment;

        await _repository.Update(request);

        // Send notification to user about approval/rejection
        if (!string.IsNullOrEmpty(request.UserEmail))
        {
            if (resolve.Status == UsernameChangeRequestStatus.Approved && request.ApprovalToken.HasValue)
            {
                await _notificationSender.SendApprovalAsync(
                    request.UserEmail,
                    request.UserUsername!,
                    request.ApprovalToken.Value);
            }
            else if (resolve.Status == UsernameChangeRequestStatus.Rejected)
            {
                await _notificationSender.SendRejectionAsync(
                    request.UserEmail,
                    request.UserUsername!,
                    resolve.Comment);
            }
        }

        return MapToEntry(request);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeApprovalInfo?> GetApprovalInfoAsync(Guid token)
    {
        var entity = await _repository.GetByApprovalToken(token);
        if (entity == null) return null;

        // A name already chosen through this link is not the same event as a link
        // that ran out, and the page says opposite things about them. Both used to
        // answer null, so both came out as "ссылка недействительна или устарела" -
        // told to someone whose name had in fact been changed.
        if (entity.Status == UsernameChangeRequestStatus.Completed)
            return UsernameChangeApprovalInfo.Used();

        // Everything else the approval can no longer be: the hourly pass has moved
        // the request to Expired, or it has not run yet and only the stored moment
        // says so, or a moderator rolled the rename back. All three leave the
        // reader the same thing to do - ask again - so they are one answer.
        if (entity.Status != UsernameChangeRequestStatus.Approved ||
            entity.ApprovalTokenExpiresUtc < _dateTimeProvider.Now)
        {
            return UsernameChangeApprovalInfo.Expired();
        }

        return UsernameChangeApprovalInfo.Ready(entity.UserUsername ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry> CompleteWithTokenAsync(Guid token, string newUsername)
    {
        var request = await _repository.GetByApprovalToken(token);
        if (request == null)
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);

        var now = _dateTimeProvider.Now;

        // Asked before the clock, and not after it: the moment stored on a spent
        // request is the one its approval carried, so a link used on the first day
        // and opened again on the third answered "срок действия истек" to someone
        // whose name had already been changed.
        if (request.Status == UsernameChangeRequestStatus.Completed)
            throw new HttpException(HttpStatusCode.Conflict, "Имя уже изменено по этой ссылке");

        // Check if token is expired
        if (request.ApprovalTokenExpiresUtc < now)
            throw new HttpException(HttpStatusCode.NotFound, "Срок действия ссылки истек");

        // Check if request is in approved state (not yet completed)
        if (request.Status != UsernameChangeRequestStatus.Approved)
            throw new HttpException(HttpStatusCode.Conflict, "Заявка не одобрена");

        // Validate username format
        if (string.IsNullOrWhiteSpace(newUsername) || !UsernameValidationRegex().IsMatch(newUsername.Trim()))
            throw new HttpException(HttpStatusCode.BadRequest, "Недопустимое имя");

        newUsername = newUsername.Trim();

        // Validate new username availability
        var usernameAvailable = await _repository.IsUsernameAvailable(newUsername);

        // FIX: Allow user to reclaim their own old username
        // Only check if the username is reserved by OTHER users
        var usernameReserved = await _historyRepository.IsUsernameReservedForOthers(newUsername, request.UserId);

        // Approval and choice are days apart, so the name can be gone by the time
        // it is asked for. The reader keeps the form and picks another one.
        if (!usernameAvailable || usernameReserved)
            throw new HttpException(HttpStatusCode.Conflict, "Это имя уже занято");

        // The history row, the new name and the resolved request go in together.
        // Written one at a time, a refusal in between left a rename half applied -
        // and, worst of the three, a renamed user whose request was still pending,
        // which is one approval spent on two renames.
        var history = new CreateUsernameHistory
        {
            UsernameHistoryId = _guidFactory.Create(),
            UserId = request.UserId,
            OldUsername = request.UserUsername!,
            NewUsername = newUsername,
            ChangedUtc = now,
            ApprovedById = request.ResolvedByUserId
        };

        request.RequestedUsername = newUsername;
        // Completed is what spends the approval; every path that could spend it
        // twice is refused above. The token itself stays on the row, because
        // erasing it is what made a used link and an unknown one the same row-less
        // lookup - and the reader of a used link was told the link was bad.
        request.Status = UsernameChangeRequestStatus.Completed;
        request.UserUsername = newUsername; // Update the local copy for MapToEntry

        await _repository.ApplyRename(request, history);

        return MapToEntry(request);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry> RollbackAsync(Guid requestId)
    {
        var currentUser = _identityProvider.Current.User;
        var request = await _repository.GetById(requestId);
        if (request == null)
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UsernameChangeRequestNotFound);

        // Can only rollback completed requests
        if (request.Status != UsernameChangeRequestStatus.Completed)
            throw new HttpException(HttpStatusCode.Conflict, "Откатить можно только завершенную смену имени");

        // Find the previous username from history
        var history = await _historyRepository.GetLatestByUserId(request.UserId);
        if (history == null)
            throw new HttpException(HttpStatusCode.Conflict, "Откат невозможен: нет истории смены имени");

        // The history entry shows: OldUsername -> NewUsername
        // To rollback, we need to set username back to OldUsername
        var previousUsername = history.OldUsername;
        var currentUsername = request.UserUsername!;

        // Check if previous username is still available (someone else might have taken it)
        // Note: UsernameFree checks Users + UsernameHistory. For rollback, we check only Users (excluding self)
        var usernameAvailable = await _repository.IsUsernameAvailable(previousUsername, request.UserId);
        if (!usernameAvailable)
            throw new HttpException(HttpStatusCode.Conflict,
                $"Откат невозможен: имя {previousUsername} уже занято");

        var now = _dateTimeProvider.Now;

        // The same three rows as the rename above, and together for the same reason.
        var rollback = new CreateUsernameHistory
        {
            UsernameHistoryId = _guidFactory.Create(),
            UserId = request.UserId,
            OldUsername = currentUsername,
            NewUsername = previousUsername,
            ChangedUtc = now,
            ApprovedById = currentUser.UserId
        };

        // Mark request as rejected (rolled back)
        request.Status = UsernameChangeRequestStatus.Rejected;
        request.ResolverComment = ResolutionComment.Join(
            request.ResolverComment,
            $"Откат модератором {currentUser.Username}: имя '{currentUsername}' отменено");
        request.UserUsername = previousUsername; // Update local copy for MapToEntry

        await _repository.ApplyRename(request, rollback);

        return MapToEntry(request);
    }

    private static UsernameChangeRequestEntry MapToEntry(UsernameChangeRequest request) => new()
    {
        RequestId = request.RequestId,
        CurrentUsername = request.UserUsername ?? string.Empty,
        UserId = request.UserId,
        RequestedUsername = request.RequestedUsername,
        Reason = request.Reason,
        Status = request.Status,
        ExpiryReason = ResolveExpiryReason(request),
        CreatedUtc = request.CreatedUtc,
        ApprovalTokenExpiresUtc = request.ApprovalTokenExpiresUtc,
        ResolvedUtc = request.ResolvedUtc,
        ResolvedByUsername = request.ResolverUsername,
        ResolverComment = request.ResolverComment
    };

    /// <summary>
    /// Expiry is reached by two different deadlines that share one status. The
    /// approval branch is the only writer of <see cref="UsernameChangeRequest.ApprovalTokenExpiresUtc"/>,
    /// and the expiry job keeps it on the row, so its presence says the request
    /// was approved before it expired.
    /// </summary>
    private static UsernameChangeExpiryReason? ResolveExpiryReason(UsernameChangeRequest request)
    {
        if (request.Status != UsernameChangeRequestStatus.Expired) return null;

        return request.ApprovalTokenExpiresUtc.HasValue
            ? UsernameChangeExpiryReason.ApprovalLapsed
            : UsernameChangeExpiryReason.Unreviewed;
    }
}
