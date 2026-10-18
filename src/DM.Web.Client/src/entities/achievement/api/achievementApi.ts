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
} from "@/shared/api/models/achievements";
import { Api } from "@/shared/api";

/**
 * Options that keep one call out of both caches.
 *
 * The four catalogues below are the endpoints the API declares
 * `Cache-Control: public, max-age=300` on, and the client no longer puts
 * `no-cache` on every request it makes. Their writes live under
 * `v1/moderation/...`, a different address, so a POST there invalidates no
 * stored copy of the list — a moderator who adds an award type and asks the
 * catalogue to reload would be handed the browser's own five-minute-old copy,
 * without the row just written, and would read it as the write having failed.
 *
 * `no-cache` on the request forbids the store and asks the origin, which is
 * what the directive is for. What was wrong before was that it was on every
 * request by default, deciding caching for endpoints it knew nothing about.
 */
const FRESH = { headers: { "Cache-Control": "no-cache" } };

/**
 * The refusal a caller takes over because its own screen carries it: the
 * moderation windows print a refused save in themselves, and the response
 * interceptor has to stay quiet or the same sentence stands in two places.
 * Every call that leaves it out is unchanged.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalFlags = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

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

  /** @param fresh Skip the caches — for a reload after an edit. */
  public getAchievementCategories(fresh = false) {
    return Api.get<AchievementCategoriesResponse>(
      "achievement-categories",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
  }

  /** @param fresh Skip the caches — for a reload after an edit. */
  public getAchievementTypes(fresh = false) {
    return Api.get<AchievementTypesResponse>(
      "achievement-types",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
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
    options?: RefusalFlags,
  ) {
    return Api.patch<AchievementCategoryEnvelope>(
      `moderation/achievement-categories/${id}`,
      body,
      options,
    );
  }

  public createAchievementType(
    body: CreateAchievementTypeRequest,
    options?: RefusalFlags,
  ) {
    return Api.post<AchievementTypeEnvelope>(
      "moderation/achievement-types",
      body,
      options,
    );
  }

  public updateAchievementType(
    id: string,
    body: UpdateAchievementTypeRequest,
    options?: RefusalFlags,
  ) {
    return Api.patch<AchievementTypeEnvelope>(
      `moderation/achievement-types/${id}`,
      body,
      options,
    );
  }

  public deleteAchievementType(id: string, options?: RefusalFlags) {
    return Api.delete(`moderation/achievement-types/${id}`, undefined, options);
  }

  // ---- Awards: public read ----

  /** @param fresh Skip the caches — for a reload after an edit. */
  public getAwardTypes(fresh = false) {
    return Api.get<AwardTypesResponse>(
      "award-types",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
  }

  /** @param fresh Skip the caches — for a reload after an edit. */
  public getContestSeries(fresh = false) {
    return Api.get<ContestSeriesResponse>(
      "contest-series",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
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

  /**
   * Full catalogs for the admin pages, inactive records included. The
   * public catalogs serve active records only, so a hidden entry vanished
   * from the very list that carries its restore button.
   */
  public getModerationAwardTypes() {
    return Api.get<AwardTypesResponse>("moderation/award-types");
  }

  public getModerationContestSeries() {
    return Api.get<ContestSeriesResponse>("moderation/contest-series");
  }

  public getModerationAchievementCategories() {
    return Api.get<AchievementCategoriesResponse>(
      "moderation/achievement-categories",
    );
  }

  public getModerationAchievementTypes() {
    return Api.get<AchievementTypesResponse>("moderation/achievement-types");
  }

  public updateAwardType(
    id: string,
    body: UpdateAwardTypeRequest,
    options?: RefusalFlags,
  ) {
    return Api.patch<AwardTypeEnvelope>(
      `moderation/award-types/${id}`,
      body,
      options,
    );
  }

  public deactivateAwardType(id: string) {
    return Api.delete(`moderation/award-types/${id}`);
  }

  public createContestSeries(
    body: CreateContestSeriesRequest,
    options?: RefusalFlags,
  ) {
    return Api.post<ContestSeriesEnvelope>(
      "moderation/contest-series",
      body,
      options,
    );
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

  public revokeUserAward(
    username: string,
    awardId: string,
    options?: RefusalFlags,
  ) {
    return Api.delete(
      `moderation/users/${encodeURIComponent(username)}/awards/${awardId}`,
      undefined,
      options,
    );
  }
})();
