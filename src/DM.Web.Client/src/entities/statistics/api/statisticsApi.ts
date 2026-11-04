import type { Envelope } from "@/shared/api/models/common";
import type { LiveStats, Leaderboards } from "@/shared/api/models/community";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

export default new (class StatisticsApi {
  public getLiveStats() {
    return Api.get<Envelope<LiveStats>>("stats", undefined, undefined, {
      skipAuth: true,
    });
  }

  // The caller sets the flags: the digest block of a topic has only the interceptor's voice.
  public getLeaderboards(
    year: number,
    month?: number,
    options?: RequestOptions,
  ) {
    const path = month
      ? `leaderboards/${year}/${month}`
      : `leaderboards/${year}`;
    return Api.get<Envelope<Leaderboards>>(path, undefined, undefined, options);
  }
})();
