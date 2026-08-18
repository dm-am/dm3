using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Community.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using FluentValidation;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <inheritdoc />
internal class UserEndorsementService : IUserEndorsementService
{
    private static readonly TimeSpan EditWindow = TimeSpan.FromDays(1);

    private readonly IValidator<CreateUserEndorsement> _createValidator;
    private readonly IValidator<UpdateUserEndorsement> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserEndorsementRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserEndorsementService(
        IValidator<CreateUserEndorsement> createValidator,
        IValidator<UpdateUserEndorsement> updateValidator,
        IIntentionManager intentionManager,
        IUserEndorsementRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<UserEndorsement> CreateAsync(CreateUserEndorsement createEndorsement)
    {
        await _createValidator.ValidateAndThrowAsync(createEndorsement);
        _intentionManager.ThrowIfForbidden(UserEndorsementIntention.Create);

        var authorId = _identityProvider.Current.User.UserId;
        var targetUserId = createEndorsement.TargetUserId;

        // The same evaluation the client asked beforehand, so the offer and
        // the answer to accepting it cannot disagree.
        var eligibility = await EvaluateEligibilityAsync(authorId, targetUserId);
        if (!eligibility.CanCreate)
        {
            throw new HttpException(eligibility.Status, eligibility.Reason!);
        }

        var entity = new CreateUserEndorsementEntity
        {
            EndorsementId = _guidFactory.Create(),
            UserId = authorId,
            TargetUserId = targetUserId,
            CreatedUtc = _dateTimeProvider.Now,
            Text = createEndorsement.Text.Trim()
        };

        try
        {
            return await _repository.CreateAsync(entity);
        }
        catch (DuplicateEntityException)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyEndorsedUser);
        }
    }

    /// <inheritdoc />
    public async Task<UserEndorsementEligibility> GetEligibilityAsync(Guid targetUserId)
    {
        // A guest is refused rather than thrown at: the question "may I?" has
        // an answer for anonymous readers, and it is "sign in first".
        if (!_intentionManager.IsAllowed(UserEndorsementIntention.Create))
        {
            return UserEndorsementEligibility.Refused(
                RefusalMessage.AuthenticationRequired, HttpStatusCode.Unauthorized);
        }

        return await EvaluateEligibilityAsync(_identityProvider.Current.User.UserId, targetUserId);
    }

    /// <summary>
    /// The rules of writing a recommendation, in one place: who is refused and
    /// with what sentence. Both the create call and the eligibility question
    /// go through here — two copies of this list would drift, and the client
    /// would offer a control the server refuses.
    /// </summary>
    /// <remarks>
    /// Authentication is not checked here; the two callers gate it their own
    /// way (a throw on create, a refusal on the query).
    /// </remarks>
    private async Task<UserEndorsementEligibility> EvaluateEligibilityAsync(Guid authorId, Guid targetUserId)
    {
        // Cheapest and most specific first: a self-recommendation is refused
        // for being one, not for whatever else the author happens to be.
        if (authorId == targetUserId)
        {
            return UserEndorsementEligibility.Refused("Нельзя рекомендовать самого себя");
        }

        if (await IsNewbieAsync(authorId))
        {
            return UserEndorsementEligibility.Refused(
                $"Чтобы рекомендовать других, нужно не меньше {ProbationPolicy.NewbiePostThreshold} постов в играх");
        }

        if (!await HavePlayedTogetherAsync(authorId, targetUserId))
        {
            return UserEndorsementEligibility.Refused(
                "Рекомендовать можно только тех, с кем вы играли в одной игре");
        }

        // One recommendation per author-recipient pair.
        if (await ExistsAsync(authorId, targetUserId))
        {
            return UserEndorsementEligibility.Refused(
                RefusalMessage.AlreadyEndorsedUser, HttpStatusCode.Conflict);
        }

        return UserEndorsementEligibility.Allowed;
    }

    /// <inheritdoc />
    public async Task<UserEndorsement> GetAsync(Guid id)
    {
        var endorsement = await _repository.GetAsync(id);
        if (endorsement == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Рекомендация не найдена");
        }

        return endorsement;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<UserEndorsement> Endorsements, PagingResult Paging)> GetListAsync(
        Guid targetUserId, PagingQuery query)
    {
        var totalCount = await _repository.CountAsync(targetUserId);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var endorsements = await _repository.GetAsync(targetUserId, pagingData);
        return (endorsements, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<UserEndorsement> Endorsements, PagingResult Paging)> GetAllAsync(
        PagingQuery query, UserEndorsementFilter? filter = null)
    {
        var totalCount = await _repository.CountAllAsync(filter);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var endorsements = await _repository.GetAllAsync(pagingData, filter);
        return (endorsements, pagingData.Result);
    }

    /// <inheritdoc />
    public Task<UserEndorsement?> GetByAuthorAsync(Guid targetUserId, Guid authorId) =>
        _repository.GetByAuthorAsync(targetUserId, authorId);

    /// <inheritdoc />
    public async Task<UserEndorsement> UpdateAsync(UpdateUserEndorsement updateEndorsement)
    {
        await _updateValidator.ValidateAndThrowAsync(updateEndorsement);
        var endorsement = await GetAsync(updateEndorsement.EndorsementId);

        _intentionManager.ThrowIfForbidden(UserEndorsementIntention.Edit, endorsement);

        // Check 24-hour edit window (admins can edit anytime)
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role != UserRole.Admin && !CanEdit(endorsement))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Рекомендацию можно править только в течение суток после создания");
        }

        if (string.IsNullOrEmpty(updateEndorsement.Text))
        {
            return endorsement;
        }

        var entity = new UpdateUserEndorsementEntity(
            endorsement.Id,
            Text: updateEndorsement.Text.Trim(),
            ModifiedUtc: _dateTimeProvider.Now,
            ModifiedByUserId: currentUser.UserId);

        return await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var endorsement = await GetAsync(id);
        _intentionManager.ThrowIfForbidden(UserEndorsementIntention.Delete, endorsement);

        var entity = new UpdateUserEndorsementEntity(
            id,
            IsRemoved: true,
            DeletedUtc: _dateTimeProvider.Now,
            DeletedByUserId: _identityProvider.Current.User.UserId);
        await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid targetUserId) =>
        _repository.ExistsAsync(authorId, targetUserId);

    /// <inheritdoc />
    public Task<bool> HavePlayedTogetherAsync(Guid userId1, Guid userId2) =>
        _repository.HavePlayedTogetherAsync(userId1, userId2);

    /// <inheritdoc />
    public bool CanEdit(UserEndorsement endorsement)
    {
        var now = _dateTimeProvider.Now;
        var editDeadline = endorsement.CreatedUtc + EditWindow;
        return now <= editDeadline;
    }

    private async Task<bool> IsNewbieAsync(Guid userId) =>
        ProbationPolicy.IsNewbie(await _repository.GetUserPostCountAsync(userId));
}
