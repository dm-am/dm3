import type {
  AwardTypesResponse,
  UserAwardsResponse,
  ContestSeriesAwardsResponse,
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
 * API client for the award/achievement catalogs and user records.
 *
 * Endpoint structure:
 *   GET  v1/award-types                              — public catalog
 *   GET  v1/contest-series                           — public catalog
 *   GET  v1/users/{u}/awards                         — public awards
 *   GET  v1/achievement-categories                   — public catalog
 *   GET  v1/achievement-types                        — public catalog
 *   GET  v1/users/{u}/achievements                   — public + lazy-eval
 *   POST/PATCH/DELETE v1/moderation/award-types       — SeniorMod
 *   POST/PATCH/DELETE v1/moderation/contest-series    — SeniorMod
 *   POST/PATCH/DELETE v1/moderation/achievement-types — SeniorMod
 *   PATCH            v1/moderation/achievement-categories/{id} — SeniorMod
 *   POST/DELETE      v1/moderation/users/{u}/awards   — SeniorMod
 */
export default new (class AchievementApi {
  // ---- Achievements: public read ----

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

  // ---- Achievements: moderation (SeniorMod) ----

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

  // ---- Awards: public read ----

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

  // ---- Awards: moderation (SeniorMod) ----

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

  public getContestSeriesAwards(seriesId: string) {
    return Api.get<ContestSeriesAwardsResponse>(
      `moderation/contest-series/${seriesId}/awards`,
    );
  }

  public revokeUserAward(username: string, awardId: string) {
    return Api.delete(
      `moderation/users/${encodeURIComponent(username)}/awards/${awardId}`,
    );
  }
})();
