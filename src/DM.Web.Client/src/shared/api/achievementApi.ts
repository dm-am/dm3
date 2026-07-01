import type {
  AwardTypesResponse,
  UserAwardsResponse,
  AchievementTypesResponse,
  AchievementCategoriesResponse,
  AchievementCategoryEnvelope,
  ContestSeriesResponse,
  ContestSeriesEnvelope,
  UserAchievementsResponse,
  AwardTypeEnvelope,
  AchievementTypeEnvelope,
  UserAwardEnvelope,
  CreateAwardTypeRequest,
  UpdateAwardTypeRequest,
  CreateAchievementTypeRequest,
  UpdateAchievementTypeRequest,
  UpdateAchievementCategoryRequest,
  CreateContestSeriesRequest,
  UpdateContestSeriesRequest,
  GrantUserAwardRequest,
} from "./models/achievements";
import Api from "./client";

/**
 * API клиент для каталогов наград/достижений и пользовательских записей.
 *
 * Endpoint-структура:
 *   GET  v1/award-types                              — публичный каталог
 *   GET  v1/contest-series                           — публичный каталог
 *   GET  v1/users/{u}/awards                         — публичные награды
 *   GET  v1/achievement-categories                   — публичный каталог
 *   GET  v1/achievement-types                        — публичный каталог
 *   GET  v1/users/{u}/achievements                   — публичные + lazy-eval
 *   POST/PATCH/DELETE v1/moderation/award-types       — SeniorMod
 *   POST/PATCH/DELETE v1/moderation/contest-series    — SeniorMod
 *   POST/PATCH/DELETE v1/moderation/achievement-types — SeniorMod
 *   PATCH            v1/moderation/achievement-categories/{id} — SeniorMod
 *   POST/DELETE      v1/moderation/users/{u}/awards   — SeniorMod
 */
export default new (class AchievementApi {
  // ---- Achievements: публичное чтение ----

  public getAchievementCategories() {
    return Api.get<AchievementCategoriesResponse>("achievement-categories");
  }

  public getAchievementTypes() {
    return Api.get<AchievementTypesResponse>("achievement-types");
  }

  public getUserAchievements(username: string) {
    return Api.get<UserAchievementsResponse>(
      `users/${encodeURIComponent(username)}/achievements`,
    );
  }

  // ---- Achievements: модерация (SeniorMod) ----

  public updateAchievementCategory(
    id: string,
    body: UpdateAchievementCategoryRequest,
  ) {
    return Api.patch<AchievementCategoryEnvelope>(
      `moderation/achievement-categories/${id}`,
      body,
    );
  }

  public createAchievementType(body: CreateAchievementTypeRequest) {
    return Api.post<AchievementTypeEnvelope>(
      "moderation/achievement-types",
      body,
    );
  }

  public updateAchievementType(id: string, body: UpdateAchievementTypeRequest) {
    return Api.patch<AchievementTypeEnvelope>(
      `moderation/achievement-types/${id}`,
      body,
    );
  }

  public deleteAchievementType(id: string) {
    return Api.delete(`moderation/achievement-types/${id}`);
  }

  // ---- Awards: публичное чтение ----

  public getAwardTypes() {
    return Api.get<AwardTypesResponse>("award-types");
  }

  public getContestSeries() {
    return Api.get<ContestSeriesResponse>("contest-series");
  }

  public getUserAwards(username: string) {
    return Api.get<UserAwardsResponse>(
      `users/${encodeURIComponent(username)}/awards`,
    );
  }

  // ---- Awards: модерация (SeniorMod) ----

  public createAwardType(body: CreateAwardTypeRequest) {
    return Api.post<AwardTypeEnvelope>("moderation/award-types", body);
  }

  public updateAwardType(id: string, body: UpdateAwardTypeRequest) {
    return Api.patch<AwardTypeEnvelope>(`moderation/award-types/${id}`, body);
  }

  public deactivateAwardType(id: string) {
    return Api.delete(`moderation/award-types/${id}`);
  }

  public createContestSeries(body: CreateContestSeriesRequest) {
    return Api.post<ContestSeriesEnvelope>("moderation/contest-series", body);
  }

  public updateContestSeries(id: string, body: UpdateContestSeriesRequest) {
    return Api.patch<ContestSeriesEnvelope>(
      `moderation/contest-series/${id}`,
      body,
    );
  }

  public deactivateContestSeries(id: string) {
    return Api.delete(`moderation/contest-series/${id}`);
  }

  public grantUserAward(username: string, body: GrantUserAwardRequest) {
    return Api.post<UserAwardEnvelope>(
      `moderation/users/${encodeURIComponent(username)}/awards`,
      body,
    );
  }

  public revokeUserAward(username: string, awardId: string) {
    return Api.delete(
      `moderation/users/${encodeURIComponent(username)}/awards/${awardId}`,
    );
  }
})();
