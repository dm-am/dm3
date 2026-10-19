import type { Envelope, ListEnvelope } from "@/shared/api/models/common";
import type {
  AchievementCategory,
  AchievementType,
  AwardType,
  ContestSeries,
  ContestSeriesAward,
  CreateAchievementTypeRequest,
  CreateContestSeriesRequest,
  GrantUserAwardRequest,
  UpdateAchievementCategoryRequest,
  UpdateAchievementTypeRequest,
  UpdateAwardTypeRequest,
  UpdateContestSeriesRequest,
  UserAchievement,
  UserAward,
} from "@/shared/api/models/achievements";
import { Api } from "@/shared/api";
import { FRESH, type RequestOptions } from "@/shared/api/interceptor";

export default new (class AchievementApi {
  // ---- Achievements: public read ----

  /** @param fresh Skip the caches — for a reload after an edit. */
  public getAchievementCategories(fresh = false) {
    return Api.get<ListEnvelope<AchievementCategory>>(
      "achievement-categories",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
  }

  /** @param fresh Skip the caches — for a reload after an edit. */
  public getAchievementTypes(fresh = false) {
    return Api.get<ListEnvelope<AchievementType>>(
      "achievement-types",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
  }

  public getUserAchievements(username: string) {
    return Api.get<ListEnvelope<UserAchievement>>(
      `users/${encodeURIComponent(username)}/achievements`,
    );
  }

  // ---- Achievements: moderation (SeniorMod) ----

  public updateAchievementCategory(
    id: string,
    body: UpdateAchievementCategoryRequest,
    options?: RequestOptions,
  ) {
    return Api.patch<Envelope<AchievementCategory>>(
      `moderation/achievement-categories/${id}`,
      body,
      options,
    );
  }

  public createAchievementType(
    body: CreateAchievementTypeRequest,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<AchievementType>>(
      "moderation/achievement-types",
      body,
      options,
    );
  }

  public updateAchievementType(
    id: string,
    body: UpdateAchievementTypeRequest,
    options?: RequestOptions,
  ) {
    return Api.patch<Envelope<AchievementType>>(
      `moderation/achievement-types/${id}`,
      body,
      options,
    );
  }

  public deleteAchievementType(id: string, options?: RequestOptions) {
    return Api.delete(`moderation/achievement-types/${id}`, undefined, options);
  }

  // ---- Awards: public read ----

  /** @param fresh Skip the caches — for a reload after an edit. */
  public getAwardTypes(fresh = false) {
    return Api.get<ListEnvelope<AwardType>>(
      "award-types",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
  }

  /** @param fresh Skip the caches — for a reload after an edit. */
  public getContestSeries(fresh = false) {
    return Api.get<ListEnvelope<ContestSeries>>(
      "contest-series",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
  }

  public getUserAwards(username: string) {
    return Api.get<ListEnvelope<UserAward>>(
      `users/${encodeURIComponent(username)}/awards`,
    );
  }

  // ---- Awards: moderation (SeniorMod) ----

  /** The admin catalogs keep the inactive records the public ones leave out. */
  public getModerationAwardTypes() {
    return Api.get<ListEnvelope<AwardType>>("moderation/award-types");
  }

  public getModerationContestSeries() {
    return Api.get<ListEnvelope<ContestSeries>>("moderation/contest-series");
  }

  public getModerationAchievementCategories() {
    return Api.get<ListEnvelope<AchievementCategory>>(
      "moderation/achievement-categories",
    );
  }

  public getModerationAchievementTypes() {
    return Api.get<ListEnvelope<AchievementType>>(
      "moderation/achievement-types",
    );
  }

  public updateAwardType(
    id: string,
    body: UpdateAwardTypeRequest,
    options?: RequestOptions,
  ) {
    return Api.patch<Envelope<AwardType>>(
      `moderation/award-types/${id}`,
      body,
      options,
    );
  }

  public createContestSeries(
    body: CreateContestSeriesRequest,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<ContestSeries>>(
      "moderation/contest-series",
      body,
      options,
    );
  }

  public updateContestSeries(id: string, body: UpdateContestSeriesRequest) {
    return Api.patch<Envelope<ContestSeries>>(
      `moderation/contest-series/${id}`,
      body,
    );
  }

  public grantUserAward(username: string, body: GrantUserAwardRequest) {
    return Api.post<Envelope<UserAward>>(
      `moderation/users/${encodeURIComponent(username)}/awards`,
      body,
    );
  }

  public getContestSeriesAwards(seriesId: string) {
    return Api.get<ListEnvelope<ContestSeriesAward>>(
      `moderation/contest-series/${seriesId}/awards`,
    );
  }

  public revokeUserAward(
    username: string,
    awardId: string,
    options?: RequestOptions,
  ) {
    return Api.delete(
      `moderation/users/${encodeURIComponent(username)}/awards/${awardId}`,
      undefined,
      options,
    );
  }
})();
