using System;
using DM.Domain.Account.Authorization;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;
using DM.Domain.Core.Users;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal class UsernameChangeService : IUsernameChangeService
{
    private readonly IValidator<CreateUsernameChangeRequest> _validator;
    private readonly IUsernameChangeRepository _repository;
    private readonly IUsernameHistoryRepository _historyRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUsernameChangeMailSender _notificationSender;
    private readonly TokenConfiguration _tokenConfig;

    public UsernameChangeService(
        IValidator<CreateUsernameChangeRequest> validator,
        IUsernameChangeRepository repository,
        IUsernameHistoryRepository historyRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IUsernameChangeMailSender notificationSender,
        IOptions<TokenConfiguration> tokenOptions)
    {
        _validator = validator;
        _repository = repository;
        _historyRepository = historyRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _notificationSender = notificationSender;
        _tokenConfig = tokenOptions.Value;
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry> CreateAsync(CreateUsernameChangeRequest request)
    {
        var currentUser = _identityProvider.RequireAuthenticated();

        await _validator.ValidateAndThrowAsync(request);

        // A pending request and an approval whose name is not chosen yet are both in flight.
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
        var currentUser = _identityProvider.RequireAuthenticated();

        var entity = await _repository.GetLatestByUserId(currentUser.UserId);
        if (entity == null) return null;

        return MapToEntry(entity);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<UsernameChangeRequestEntry>> GetPendingRequestsAsync() =>
        _repository.GetPendingRequests();

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

        // Completed and Expired are reached by the flow itself, not by a moderator.
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
            request.ApprovalToken = _guidFactory.Create();
            // The letter says the same hours: they reach it from the same setting.
            request.ApprovalTokenExpiresUtc = now + TimeSpan.FromHours(_tokenConfig.UsernameChangeApprovalTokenLifetimeHours);
        }

        request.Status = resolve.Status;
        request.ResolvedUtc = now;
        request.ResolvedByUserId = currentUser.UserId;
        request.ResolverComment = resolve.Comment;

        await _repository.Update(request);

        if (!string.IsNullOrEmpty(request.UserEmail))
        {
            if (resolve.Status == UsernameChangeRequestStatus.Approved && request.ApprovalToken.HasValue)
            {
                await _notificationSender.SendApprovalAsync(
                    request.UserEmail, request.ApprovalToken.Value);
            }
            else if (resolve.Status == UsernameChangeRequestStatus.Rejected)
            {
                await _notificationSender.SendRejectionAsync(
                    request.UserEmail,
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

        // A name already chosen through this link is not a link that ran out.
        if (entity.Status == UsernameChangeRequestStatus.Completed)
            return UsernameChangeApprovalInfo.Used();

        // Expired by the hourly pass or by the stored moment, or rolled back: all
        // three leave the reader asking again.
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

        // Asked before the clock: a spent request keeps the moment its approval carried.
        if (request.Status == UsernameChangeRequestStatus.Completed)
            throw new HttpException(HttpStatusCode.Conflict, "Имя уже изменено по этой ссылке");

        if (request.ApprovalTokenExpiresUtc < now)
            throw new HttpException(HttpStatusCode.NotFound, "Срок действия ссылки истек");

        if (request.Status != UsernameChangeRequestStatus.Approved)
            throw new HttpException(HttpStatusCode.Conflict, "Заявка не одобрена");

        if (string.IsNullOrWhiteSpace(newUsername) || !UsernamePolicy.IsValid(newUsername.Trim()))
            throw new HttpException(HttpStatusCode.BadRequest, "Недопустимое имя");

        newUsername = newUsername.Trim();

        var usernameAvailable = await _repository.IsUsernameAvailable(newUsername);

        // The user may reclaim their own old username
        var usernameReserved = await _historyRepository.IsUsernameReservedForOthers(newUsername, request.UserId);

        // Approval and choice are days apart, so the name can be gone by the time
        // it is asked for. The reader keeps the form and picks another one.
        if (!usernameAvailable || usernameReserved)
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.UsernameTaken);

        // The history row, the new name and the resolved request go in together,
        // so a refusal cannot leave a rename half applied.
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
        // Completed spends the approval; the token stays so that a used link is
        // told apart from an unknown one.
        request.Status = UsernameChangeRequestStatus.Completed;
        request.UserUsername = newUsername; // Update the local copy for MapToEntry

        try
        {
            await _repository.ApplyRename(request, history);
        }
        catch (DuplicateEntityException)
        {
            // A name taken between the reads above and this write: the pre-check's answer.
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.UsernameTaken);
        }

        return MapToEntry(request);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequestEntry> RollbackAsync(Guid requestId)
    {
        // Renames another user, so the method guards itself rather than trusting
        // whatever endpoint ends up calling it.
        _intentionManager.ThrowIfForbidden(AccountIntention.RollbackUsernameChange);

        var currentUser = _identityProvider.Current.User;
        var request = await _repository.GetById(requestId);
        if (request == null)
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UsernameChangeRequestNotFound);

        if (request.Status != UsernameChangeRequestStatus.Completed)
            throw new HttpException(HttpStatusCode.Conflict, "Откатить можно только завершенную смену имени");

        var history = await _historyRepository.GetLatestByUserId(request.UserId);
        if (history == null)
            throw new HttpException(HttpStatusCode.Conflict, "Откат невозможен: нет истории смены имени");

        var previousUsername = history.OldUsername;
        var currentUsername = request.UserUsername!;

        // Only Users is asked, excluding self: the history holds the user's own old name
        var usernameAvailable = await _repository.IsUsernameAvailable(previousUsername, request.UserId);
        if (!usernameAvailable)
            throw new HttpException(HttpStatusCode.Conflict,
                RefusalMessage.UsernameRollbackBlocked(previousUsername));

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

        request.Status = UsernameChangeRequestStatus.Rejected;
        request.ResolverComment = ResolutionComment.Join(
            request.ResolverComment,
            $"Откат модератором {currentUser.Username}: имя '{currentUsername}' отменено");
        request.UserUsername = previousUsername; // Update local copy for MapToEntry

        try
        {
            await _repository.ApplyRename(request, rollback);
        }
        catch (DuplicateEntityException)
        {
            throw new HttpException(HttpStatusCode.Conflict,
                RefusalMessage.UsernameRollbackBlocked(previousUsername));
        }

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
