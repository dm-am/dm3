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

        // Newbies cannot create user endorsements
        if (await IsNewbieAsync(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                $"Чтобы рекомендовать других, нужно не меньше {ProbationPolicy.NewbiePostThreshold} постов в играх");
        }

        // Can't endorse yourself
        if (authorId == targetUserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя рекомендовать самого себя");
        }

        // Check if users have played together
        var havePlayedTogether = await HavePlayedTogetherAsync(authorId, targetUserId);
        if (!havePlayedTogether)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "Рекомендовать можно только тех, с кем вы играли в одной игре");
        }

        // Check if already endorsed
        if (await ExistsAsync(authorId, targetUserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyEndorsedUser);
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
