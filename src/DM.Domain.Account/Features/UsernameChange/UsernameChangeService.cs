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

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal partial class UsernameChangeService : IUsernameChangeService
{
    // Forbidden: control chars, HTML/URL unsafe, quotes, brackets, special chars, zero-width
    // Whitespace: not at start/end, not consecutive
    // See: docs/conventions/USERNAME_POLICY.md
    [GeneratedRegex(@"^(?!\s)(?!.*\s$)(?!.*\s{2})[^\p{Cc}<>""'`\\/@?#%&\[\](){}=~!$^*+|;:\u200B-\u200F\u2028-\u202F\uFEFF]{2,20}$")]
    private static partial Regex UsernameValidationRegex();

    private readonly IValidator<CreateUsernameChangeRequest> _validator;
    private readonly IUsernameChangeRepository _repository;
    private readonly IUsernameHistoryRepository _historyRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUsernameChangeMailSender _notificationSender;

    public UsernameChangeService(
        IValidator<CreateUsernameChangeRequest> validator,
        IUsernameChangeRepository repository,
        IUsernameHistoryRepository historyRepository,
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        IUsernameChangeMailSender notificationSender)
    {
        _validator = validator;
        _repository = repository;
        _historyRepository = historyRepository;
        _identityProvider = identityProvider;
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

        // Check no pending/approved request exists
        var existing = await _repository.GetPendingByUserId(currentUser.UserId);
        if (existing != null)
            throw new HttpException(HttpStatusCode.Conflict, "Заявка на смену имени уже отправлена");

        var now = _dateTimeProvider.Now;
        var dto = new UsernameChangeRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = currentUser.UserId,
            // Username is NOT set here - user chooses it after approval
            RequestedUsername = null,
            Reason = request.Reason,
            Status = UsernameChangeRequestStatus.Pending,
            CreatedUtc = now
        };

        await _repository.Add(dto);

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

        var now = _dateTimeProvider.Now;

        if (resolve.Status == UsernameChangeRequestStatus.Approved)
        {
            // Generate approval token (user will use this to complete the change)
            request.ApprovalToken = Guid.NewGuid();
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
    public async Task<UsernameChangeRequestEntry?> GetByApprovalTokenAsync(Guid token)
    {
        var entity = await _repository.GetByApprovalToken(token);
        if (entity == null) return null;

        // Check if token is expired
        var now = _dateTimeProvider.Now;
        if (entity.ApprovalTokenExpiresUtc < now)
            return null;

        return MapToEntry(entity);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry> CompleteWithTokenAsync(Guid token, string newUsername)
    {
        var request = await _repository.GetByApprovalToken(token);
        if (request == null)
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);

        var now = _dateTimeProvider.Now;

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

        if (!usernameAvailable || usernameReserved)
            throw new HttpException(HttpStatusCode.Conflict, "Имя недоступно");

        // Record history
        await _historyRepository.Add(new CreateUsernameHistory
        {
            UsernameHistoryId = Guid.NewGuid(),
            UserId = request.UserId,
            OldUsername = request.UserUsername!,
            NewUsername = newUsername,
            ChangedUtc = now,
            ApprovedById = request.ResolvedByUserId
        });

        // Update user's username
        await _repository.UpdateUserUsername(request.UserId, newUsername);

        // Update request
        request.RequestedUsername = newUsername;
        request.Status = UsernameChangeRequestStatus.Completed;
        request.ApprovalToken = null; // Invalidate token
        request.UserUsername = newUsername; // Update the local copy for MapToEntry

        await _repository.Update(request);

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

        // Record the rollback in history
        await _historyRepository.Add(new CreateUsernameHistory
        {
            UsernameHistoryId = Guid.NewGuid(),
            UserId = request.UserId,
            OldUsername = currentUsername,
            NewUsername = previousUsername,
            ChangedUtc = now,
            ApprovedById = currentUser.UserId
        });

        // Update username back to previous
        await _repository.UpdateUserUsername(request.UserId, previousUsername);

        // Mark request as rejected (rolled back)
        request.Status = UsernameChangeRequestStatus.Rejected;
        request.ResolverComment = (request.ResolverComment ?? "") +
            $" | Откат модератором {currentUser.Username}: имя '{currentUsername}' отменено";
        request.UserUsername = previousUsername; // Update local copy for MapToEntry

        await _repository.Update(request);

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
        CreatedUtc = request.CreatedUtc,
        ApprovalTokenExpiresUtc = request.ApprovalTokenExpiresUtc,
        ResolvedUtc = request.ResolvedUtc,
        ResolvedByUsername = request.ResolverUsername,
        ResolverComment = request.ResolverComment
    };
}
