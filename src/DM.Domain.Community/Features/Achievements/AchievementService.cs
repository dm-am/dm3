using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Icons;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Community.Features.Achievements;

/// <inheritdoc />
internal class AchievementService : IAchievementService
{
    private readonly IAchievementRepository _repository;
    private readonly IUserLookupService _userLookup;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<CreateAchievementType> _createTypeValidator;
    private readonly IValidator<UpdateAchievementType> _updateTypeValidator;
    private readonly IValidator<UpdateAchievementCategory> _updateCategoryValidator;

    public AchievementService(
        IAchievementRepository repository,
        IUserLookupService userLookup,
        IDateTimeProvider clock,
        IValidator<CreateAchievementType> createTypeValidator,
        IValidator<UpdateAchievementType> updateTypeValidator,
        IValidator<UpdateAchievementCategory> updateCategoryValidator)
    {
        _repository = repository;
        _userLookup = userLookup;
        _clock = clock;
        _createTypeValidator = createTypeValidator;
        _updateTypeValidator = updateTypeValidator;
        _updateCategoryValidator = updateCategoryValidator;
    }

    // ---- Categories ----

    /// <inheritdoc />
    public Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        _repository.GetCategoriesAsync(includeInactive, ct);

    /// <inheritdoc />
    public async Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default)
    {
        await _updateCategoryValidator.ValidateAndThrowAsync(update, ct);
        if (update.IconName != null) EnsureIconValid(update.IconName);
        _ = await _repository.GetCategoryAsync(update.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Achievement category not found");
        return await _repository.UpdateCategoryAsync(update, ct);
    }

    // ---- Tiers ----

    /// <inheritdoc />
    public Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        _repository.GetTypesAsync(includeInactive, ct);

    /// <inheritdoc />
    public async Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default)
    {
        await _createTypeValidator.ValidateAndThrowAsync(create, ct);

        // Without this the unknown category surfaced as a foreign-key violation,
        // i.e. a 500, while the endpoint advertised 400 for exactly this case.
        _ = await _repository.GetCategoryAsync(create.AchievementCategoryId, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Achievement category not found");

        var existing = await _repository.GetTypeByCodeAsync(create.Code, ct);
        if (existing != null)
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Achievement type with code '{create.Code}' already exists");
        }

        return await _repository.CreateTypeAsync(create, ct);
    }

    /// <inheritdoc />
    public async Task<AchievementType> UpdateTypeAsync(UpdateAchievementType update, CancellationToken ct = default)
    {
        await _updateTypeValidator.ValidateAndThrowAsync(update, ct);
        _ = await _repository.GetTypeAsync(update.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Achievement type not found");
        return await _repository.UpdateTypeAsync(update, ct);
    }

    /// <inheritdoc />
    public async Task DeleteTypeAsync(Guid id, CancellationToken ct = default)
    {
        // The repository returns silently when the row is absent, so without this
        // a delete of a nonexistent tier answered 204 and the declared 404 could
        // never fire.
        _ = await _repository.GetTypeAsync(id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Achievement type not found");
        await _repository.DeleteTypeAsync(id, ct);
    }

    // ---- User earnings ----

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<UserAchievement>> GetUserAchievementsAsync(string username, CancellationToken ct = default)
    {
        var user = await _userLookup.GetAsync(username);

        var earned = await _repository.GetUserAchievementsAsync(user.UserId, ct);
        var earnedTypeIds = earned.Select(a => a.Type.Id).ToHashSet();

        var types = await _repository.GetTypesAsync(includeInactive: false, ct);
        var now = _clock.Now;
        var newlyGranted = false;

        foreach (var type in types)
        {
            if (earnedTypeIds.Contains(type.Id)) continue;

            // Metric lives on the parent category (SSOT).
            var value = AchievementMetricResolver.GetValue(type.Category.Metric, user, now);
            if (value < type.Threshold) continue;

            // TryGrantAsync is idempotent — the UNIQUE constraint silently
            // rejects a duplicate in case of a race with the event evaluator.
            if (await _repository.TryGrantAsync(user.UserId, type.Id, now, ct))
            {
                newlyGranted = true;
            }
        }

        // If anything was granted, re-read earned to return
        // the full list in one pass (fresh records with up-to-date
        // EarnedUtc + related Type+Category).
        return newlyGranted
            ? await _repository.GetUserAchievementsAsync(user.UserId, ct)
            : earned;
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
