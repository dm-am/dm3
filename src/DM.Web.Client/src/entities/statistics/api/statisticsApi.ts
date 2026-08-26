import type { Envelope } from "@/shared/api/models/common";
import type { LiveStats, Leaderboards } from "@/shared/api/models/community";
import { Api } from "@/shared/api";

/** Sitewide counters and the periodic leaderboards built from them. */
export default new (class StatisticsApi {
  /** Get live community statistics (public endpoint, no auth needed) */
  public getLiveStats() {
    return Api.get<Envelope<LiveStats>>("stats", undefined, undefined, {
      skipAuth: true,
    });
  }

  /**
   * Get leaderboards for a calendar period (public endpoint).
   * GET /v1/leaderboards/{year} for a year, /v1/leaderboards/{year}/{month}
   * for a month. Response is wrapped in Envelope ({ resource: ... }).
   */
  public getLeaderboards(year: number, month?: number) {
    const path = month
      ? `leaderboards/${year}/${month}`
      : `leaderboards/${year}`;
    return Api.get<Envelope<Leaderboards>>(path);
  }
})();
