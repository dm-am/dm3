/**
 * @vitest-environment jsdom
 */

/**
 * A catalogue reloaded after an edit is read from the origin, not from a cache.
 *
 * The API declares `Cache-Control: public, max-age=300` on five catalogues, and
 * the client no longer sends `no-cache` on every request. All five are written
 * through `v1/moderation/...` — a different address — so nothing invalidates
 * the stored copy of the list: the moderator adds an award type or a game tag,
 * the screen re-reads the catalogue, and the browser hands back its own
 * pre-edit copy for the next five minutes. That reads exactly like a write that
 * failed, and unlike the module-level caches it survives a reload.
 *
 * Two facts, because one alone would be satisfied by the wrong fix: the first
 * pins that the reload asks for a fresh copy, the second that the ordinary load
 * does not — putting the header back on every call would be the default header
 * this whole change removed, one module further down.
 *
 * The list below is not this file's own idea of which endpoints are cacheable.
 * ResponseCachePolicyShould reads the policy off the API assembly and fails if
 * a path carrying it is missing here, which is how `games/tags` — cacheable,
 * moderator-written, and left out when the other four were fixed — was found.
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
    | Record<string, string>
    | undefined;
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

/** Re-reads all five the way the screens that edit them do. */
async function reloadEveryCatalogue(): Promise<void> {
  const { useAchievementCatalog } = await import(
    "./entities/achievement/model/useAchievementCatalog"
  );
  const { useContestSeries } = await import(
    "./entities/achievement/model/useContestSeries"
  );
  const { useGamesStore } = await import("./entities/game/model/store");

  await useAchievementCatalog().reload();
  await useContestSeries().reload();
  await useGamesStore().fetchTags(true);
}

/** Reads all five the way an ordinary visitor's screen does. */
async function loadEveryCatalogue(): Promise<void> {
  const { useAchievementCatalog } = await import(
    "./entities/achievement/model/useAchievementCatalog"
  );
  const { useContestSeries } = await import(
    "./entities/achievement/model/useContestSeries"
  );
  const { useGamesStore } = await import("./entities/game/model/store");

  await useAchievementCatalog().load();
  await useContestSeries().load();
  await useGamesStore().fetchTags();
}

describe("the publicly cacheable catalogues", () => {
  beforeEach(() => {
    get.mockClear();
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it("are re-read from the origin after an edit", async () => {
    await reloadEveryCatalogue();

    for (const path of CATALOGUES) {
      expect(bypassesCache(path), `${path} was reloaded from a cache`).toBe(
        true,
      );
    }
  });

  it("are read normally on the first load", async () => {
    await loadEveryCatalogue();

    for (const path of CATALOGUES) {
      expect(
        bypassesCache(path),
        `${path} cancels the cache policy on an ordinary read`,
      ).toBe(false);
    }
  });
});
