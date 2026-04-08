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
using Npgsql;

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
    private readonly IProbationConfiguration _probationConfig;

    public UserEndorsementService(
        IValidator<CreateUserEndorsement> createValidator,
        IValidator<UpdateUserEndorsement> updateValidator,
        IIntentionManager intentionManager,
        IUserEndorsementRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IProbationConfiguration probationConfig)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _probationConfig = probationConfig;
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
                "You need at least 100 game posts to create user endorsements");
        }

        // Can't endorse yourself
        if (authorId == targetUserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You cannot endorse yourself");
        }

        // Check if users have played together
        var havePlayedTogether = await HavePlayedTogetherAsync(authorId, targetUserId);
        if (!havePlayedTogether)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You can only endorse users you have played with in the same game");
        }

        // Check if already endorsed
        if (await ExistsAsync(authorId, targetUserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already endorsed this user");
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
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already endorsed this user");
        }
    }

    /// <inheritdoc />
    public async Task<UserEndorsement> GetAsync(Guid id)
    {
        var endorsement = await _repository.GetAsync(id);
        if (endorsement == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Endorsement not found");
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
                "Endorsements can only be edited within 24 hours of creation");
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

        var entity = new UpdateUserEndorsementEntity(id, IsRemoved: true);
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

    private async Task<bool> IsNewbieAsync(Guid userId)
    {
        var postCount = await _repository.GetUserPostCountAsync(userId);
        return postCount < _probationConfig.NewbiePostThreshold;
    }
}
