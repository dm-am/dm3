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

namespace DM.Domain.Community.Features.Achievements;

/// <inheritdoc />
internal class AchievementService : IAchievementService
{
    private readonly IAchievementRepository _repository;
    private readonly IUserLookupService _userLookup;
    private readonly IDateTimeProvider _clock;

    public AchievementService(
        IAchievementRepository repository,
        IUserLookupService userLookup,
        IDateTimeProvider clock)
    {
        _repository = repository;
        _userLookup = userLookup;
        _clock = clock;
    }

    // ---- Категории ----

    /// <inheritdoc />
    public Task<IReadOnlyCollection<AchievementCategory>> GetCategoriesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        _repository.GetCategoriesAsync(includeInactive, ct);

    /// <inheritdoc />
    public async Task<AchievementCategory> UpdateCategoryAsync(UpdateAchievementCategory update, CancellationToken ct = default)
    {
        if (update.IconName != null) EnsureIconValid(update.IconName);
        _ = await _repository.GetCategoryAsync(update.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Achievement category not found");
        return await _repository.UpdateCategoryAsync(update, ct);
    }

    // ---- Тиры ----

    /// <inheritdoc />
    public Task<IReadOnlyCollection<AchievementType>> GetTypesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        _repository.GetTypesAsync(includeInactive, ct);

    /// <inheritdoc />
    public async Task<AchievementType> CreateTypeAsync(CreateAchievementType create, CancellationToken ct = default)
    {
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
        _ = await _repository.GetTypeAsync(update.Id, ct)
            ?? throw new HttpException(HttpStatusCode.NotFound, "Achievement type not found");
        return await _repository.UpdateTypeAsync(update, ct);
    }

    /// <inheritdoc />
    public Task DeleteTypeAsync(Guid id, CancellationToken ct = default) =>
        _repository.DeleteTypeAsync(id, ct);

    // ---- Получения пользователем ----

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

            // Метрика на категории-родителе (SSOT).
            var value = AchievementMetricResolver.GetValue(type.Category.Metric, user, now);
            if (value < type.Threshold) continue;

            // TryGrantAsync идемпотентен — UNIQUE-constraint молча
            // отобьет повтор в случае гонки с event evaluator'ом.
            if (await _repository.TryGrantAsync(user.UserId, type.Id, now, ct))
            {
                newlyGranted = true;
            }
        }

        // Если что-то начислено — перечитаем earned, чтобы вернуть
        // полный список одним проходом (свежие записи с актуальным
        // EarnedUtc + связанным Type+Category).
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
