/**
 * @vitest-environment jsdom
 */

/**
 * A catalogue reloaded after an edit is read from the origin, not from a cache.
 *
 * The API declares `Cache-Control: public, max-age=300` on five catalogues, all
 * written through `v1/moderation/...`, a different address, so nothing
 * invalidates the stored copy: without a fresh read the moderator's edit looks
 * like a write that failed. The reload asks for a fresh copy and the ordinary
 * load does not. ResponseCachePolicyShould reads the policy off the API
 * assembly and fails if a path carrying it is missing here.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const get = vi.fn();

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    Api: {
      get: (...args: unknown[]) => {
        get(...args);
        return Promise.resolve({ data: { resources: [] }, error: null });
      },
    },
  };
});

/** The request options of the call to a given path. */
function optionsFor(path: string): Record<string, unknown> | undefined {
  const call = get.mock.calls.find((args) => args[0] === path);
  expect(call, `no request to ${path}`).toBeDefined();
  return call![3] as Record<string, unknown> | undefined;
}

/** Whether a call asked the caches to stand aside. */
function bypassesCache(path: string): boolean {
  const headers = optionsFor(path)?.headers as
    Record<string, string> | undefined;
  return headers?.["Cache-Control"] === "no-cache";
}

/** Every path the API answers `public, max-age=300` on. */
const CATALOGUES = [
  "achievement-categories",
  "achievement-types",
  "award-types",
  "contest-series",
  "games/tags",
];

/**
 * Reads all five the way a screen does: an ordinary one loads them, one that
 * edits them reloads them.
 */
async function readEveryCatalogue(how: "load" | "reload"): Promise<void> {
  const { useAchievementCatalog } =
    await import("../entities/achievement/model/useAchievementCatalog");
  const { useContestSeries } =
    await import("../entities/achievement/model/useContestSeries");
  const { useGamesStore } = await import("../entities/game/model/store");

  await useAchievementCatalog()[how]();
  await useContestSeries()[how]();
  await useGamesStore().fetchTags(how === "reload");
}

describe("the publicly cacheable catalogues", () => {
  beforeEach(() => {
    get.mockClear();
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it("are re-read from the origin after an edit", async () => {
    await readEveryCatalogue("reload");

    for (const path of CATALOGUES) {
      expect(bypassesCache(path), `${path} was reloaded from a cache`).toBe(
        true,
      );
    }
  });

  it("are read normally on the first load", async () => {
    // A no-cache on every request would pass the test above as well.
    await readEveryCatalogue("load");

    for (const path of CATALOGUES) {
      expect(
        bypassesCache(path),
        `${path} cancels the cache policy on an ordinary read`,
      ).toBe(false);
    }
  });
});
