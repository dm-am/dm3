/**
 * @vitest-environment node
 */

/**
 * Who answers for a failed read of the leaderboards.
 *
 * Two screens read this endpoint and they answer differently. The statistics
 * page has a banner over the boards and it speaks for every status, so a period
 * the server never answered for is not drawn as eight empty boards under
 * "Данных за период пока нет"; with the response interceptor also raising a
 * plate for a 403, a 429 and a 5xx, the reader got that refusal twice. The
 * digest block on a topic page says nothing at all about a failure — it drops
 * the error and shows empty cards — so the plate is the whole answer it has.
 *
 * One endpoint, opposite arrangements: that is why the flag is the caller's and
 * not the method's. These two assert the method keeps it that way — it carries
 * what the caller hands it, and invents nothing for a caller that hands it
 * nothing.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", () => ({
  Api: { get: vi.fn().mockResolvedValue({ data: null, error: null }) },
}));

import { Api } from "@/shared/api";
import statisticsApi from "./statisticsApi";

/** The options the client was handed for the last read it was given. */
const optionsOfLastCall = () =>
  vi.mocked(Api.get).mock.calls.at(-1)?.[3] as
    | { ownsRefusal?: boolean; ownsRateLimit?: boolean }
    | undefined;

describe("statisticsApi.getLeaderboards", () => {
  beforeEach(() => vi.mocked(Api.get).mockClear());

  it("hands the client the flags its caller set", async () => {
    await statisticsApi.getLeaderboards(2026, 7, {
      ownsRefusal: true,
      ownsRateLimit: true,
    });

    expect(vi.mocked(Api.get)).toHaveBeenLastCalledWith(
      "leaderboards/2026/7",
      undefined,
      undefined,
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });

  it("leaves the refusal to the interceptor for a caller that is silent", async () => {
    await statisticsApi.getLeaderboards(2026);

    expect(optionsOfLastCall()).toBeUndefined();
  });
});
