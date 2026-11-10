/**
 * @vitest-environment node
 */

// A refused read keeps nothing, so it never reads as an empty catalogue and
// the next call asks again.
import { describe, it, expect, vi, beforeEach } from "vitest";

const api = vi.hoisted(() => ({
  getAchievementCategories: vi.fn(),
  getAchievementTypes: vi.fn(),
  getModerationAchievementCategories: vi.fn(),
  getModerationAchievementTypes: vi.fn(),
}));

vi.mock("@/entities/achievement/api/achievementApi", () => ({ default: api }));

const listed = <T>(resources: T[]) =>
  Promise.resolve({ data: { resources }, error: null });
const refused = () =>
  Promise.resolve({ data: null, error: { status: 500, title: "Ошибка" } });

const CATEGORY = { id: "c-1" };

/** The module keeps its catalogues between calls, so every case starts on a fresh copy. */
async function fresh() {
  vi.resetModules();
  const { useAchievementCatalog } = await import("./useAchievementCatalog");
  return useAchievementCatalog();
}

beforeEach(() => {
  for (const method of Object.values(api)) method.mockReset();
  api.getAchievementCategories.mockImplementation(refused);
  api.getAchievementTypes.mockImplementation(() => listed([]));
  api.getModerationAchievementCategories.mockImplementation(refused);
  api.getModerationAchievementTypes.mockImplementation(() => listed([]));
});

describe("useAchievementCatalog", () => {
  it("keeps nothing of a refused public read and reads again", async () => {
    const catalog = await fresh();
    await catalog.load();

    expect(catalog.failed.value).toBe(true);
    expect(catalog.categories.value).toBeNull();
    expect(catalog.types.value).toBeNull();

    api.getAchievementCategories.mockImplementation(() => listed([CATEGORY]));
    await catalog.load();

    expect(api.getAchievementCategories).toHaveBeenCalledTimes(2);
    expect(catalog.failed.value).toBe(false);
    expect(catalog.categories.value).toEqual([CATEGORY]);
  });

  it("keeps nothing of a refused admin read and reads again", async () => {
    const catalog = await fresh();
    await catalog.loadAdmin();

    expect(catalog.adminFailed.value).toBe(true);
    expect(catalog.adminCategories.value).toBeNull();
    expect(catalog.adminTypes.value).toBeNull();

    api.getModerationAchievementCategories.mockImplementation(() =>
      listed([CATEGORY]),
    );
    await catalog.loadAdmin();

    expect(api.getModerationAchievementCategories).toHaveBeenCalledTimes(2);
    expect(catalog.adminFailed.value).toBe(false);
    expect(catalog.adminCategories.value).toEqual([CATEGORY]);
  });
});
