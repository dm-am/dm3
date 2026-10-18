import type { Envelope } from "@/shared/api/models/common";
import type { LiveStats, Leaderboards } from "@/shared/api/models/community";
import { Api } from "@/shared/api";

/**
 * The refusal a caller takes over because its own screen carries it.
 *
 * A screen that answers a failure where the reader is looking — the banner in
 * place of the boards, the strip in place of a list — is the one carrier of
 * that failure, and the response interceptor has to stay quiet or the same
 * sentence stands in two places at once. The method below passes this through
 * untouched; a call that leaves it out is unchanged, because the client then
 * sees nothing and the interceptor speaks as it always did.
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
   *
   * @param options Set by the statistics page, which stands a banner over the
   * boards for a failed read and needs no plate beside it. The digest block on
   * a topic page reads the same endpoint and says nothing at all about a
   * failure, so it leaves this out and keeps the interceptor's voice — which is
   * why the flag belongs to the caller and not to this method.
   */
  public getLeaderboards(year: number, month?: number, options?: RefusalFlags) {
    const path = month
      ? `leaderboards/${year}/${month}`
      : `leaderboards/${year}`;
    return Api.get<Envelope<Leaderboards>>(path, undefined, undefined, options);
  }
})();
