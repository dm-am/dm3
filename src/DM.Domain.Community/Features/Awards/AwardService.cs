using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Icons;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;

namespace DM.Domain.Community.Features.Awards;

/// <inheritdoc />
internal class AwardService : IAwardService
{
    private readonly IAwardRepository _repository;
    private readonly IUserLookupService _userLookup;
    private readonly IIdentityProvider _identity;
    private readonly IEventProducer _eventProducer;

    public AwardService(
        IAwardRepository repository,
        IUserLookupService userLookup,
        IIdentityProvider identity,
        IEventProducer eventProducer)
    {
        _repository = repository;
        _userLookup = userLookup;
        _identity = identity;
        _eventProducer = eventProducer;
    }

    // ---- Award types ----

    public Task<IReadOnlyCollection<AwardType>> GetTypesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        _repository.GetTypesAsync(includeInactive, ct);

    public async Task<AwardType> CreateTypeAsync(CreateAwardType create, CancellationToken ct = default)
    {
        EnsureIconValid(create.IconName);
        var existing = await _repository.GetTypeByCodeAsync(create.Code, ct);
        if (existing != null)
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Award type with code '{create.Code}' already exists");
        }
        return await _repository.CreateTypeAsync(create, ct);
    }

    public async Task<AwardType> UpdateTypeAsync(UpdateAwardType update, CancellationToken ct = default)
    {
        if (update.IconName != null) EnsureIconValid(update.IconName);
        _ = await _repository.GetTypeAsync(update.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Award type not found");
        return await _repository.UpdateTypeAsync(update, ct);
    }

    public Task DeactivateTypeAsync(Guid id, CancellationToken ct = default) =>
        _repository.UpdateTypeAsync(new UpdateAwardType { Id = id, IsActive = false }, ct);

    // ---- Contest series ----

    public Task<IReadOnlyCollection<ContestSeries>> GetSeriesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        _repository.GetSeriesAsync(includeInactive, ct);

    public Task<ContestSeries?> GetSeriesAsync(Guid id, CancellationToken ct = default) =>
        _repository.GetSeriesAsync(id, ct);

    public Task<ContestSeries> CreateSeriesAsync(CreateContestSeries create, CancellationToken ct = default) =>
        _repository.CreateSeriesAsync(create, ct);

    public async Task<ContestSeries> UpdateSeriesAsync(UpdateContestSeries update, CancellationToken ct = default)
    {
        _ = await _repository.GetSeriesAsync(update.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Contest series not found");
        return await _repository.UpdateSeriesAsync(update, ct);
    }

    public Task DeactivateSeriesAsync(Guid id, CancellationToken ct = default) =>
        _repository.UpdateSeriesAsync(new UpdateContestSeries { Id = id, IsActive = false }, ct);

    // ---- Grants ----

    public async Task<IReadOnlyCollection<UserAward>> GetUserAwardsAsync(string username, CancellationToken ct = default)
    {
        var user = await _userLookup.GetAsync(username);
        return await _repository.GetUserAwardsAsync(user.UserId, ct);
    }

    public async Task<UserAward> GrantAsync(string username, Guid awardTypeId, Guid? contestSeriesId, string? workUrl, CancellationToken ct = default)
    {
        var user = await _userLookup.GetAsync(username);

        var type = await _repository.GetTypeAsync(awardTypeId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Award type not found");
        if (!type.IsActive)
        {
            throw new HttpException(HttpStatusCode.BadRequest, "Cannot grant an inactive award type");
        }

        if (contestSeriesId.HasValue)
        {
            var series = await _repository.GetSeriesAsync(contestSeriesId.Value, ct)
                ?? throw new HttpException(HttpStatusCode.NotFound, "Contest series not found");
            if (!series.IsActive)
            {
                throw new HttpException(HttpStatusCode.BadRequest, "Cannot grant in an inactive contest series");
            }
        }

        var create = new CreateUserAward
        {
            UserId = user.UserId,
            AwardTypeId = awardTypeId,
            ContestSeriesId = contestSeriesId,
            WorkUrl = string.IsNullOrWhiteSpace(workUrl) ? null : workUrl.Trim(),
        };
        var granted = await _repository.CreateAsync(create, _identity.Current.User.UserId, ct);

        // Publish notification event — the worker generator turns it into
        // a UserNotification record for the recipient.
        await _eventProducer.SendAsync(EventType.AwardGranted, granted.Id);

        return granted;
    }

    public async Task RevokeAsync(Guid awardId, CancellationToken ct = default)
    {
        _ = await _repository.GetAsync(awardId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Award not found");

        await _repository.RevokeAsync(awardId, _identity.Current.User.UserId, ct);
    }

    private static void EnsureIconValid(string iconName)
    {
        if (!GameIconCatalog.IsValid(iconName))
        {
            throw new HttpException(HttpStatusCode.BadRequest,
                $"Unknown icon name '{iconName}'. Add it to the game-icons sprite first.");
        }
    }
}
