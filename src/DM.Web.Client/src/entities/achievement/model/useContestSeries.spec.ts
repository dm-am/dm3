/**
 * @vitest-environment node
 */

// A reload after an edit keeps the rows shown until the answer replaces them,
// and a refused read says so instead of reading as an empty catalog.
import { describe, it, expect, vi, beforeEach } from "vitest";

const api = vi.hoisted(() => ({
  getAwardTypes: vi.fn(),
  getContestSeries: vi.fn(),
  getModerationAwardTypes: vi.fn(),
  getModerationContestSeries: vi.fn(),
}));

vi.mock("@/entities/achievement/api/achievementApi", () => ({ default: api }));

const listed = <T>(resources: T[]) =>
  Promise.resolve({ data: { resources }, error: null });
const refused = () =>
  Promise.resolve({ data: null, error: { status: 500, title: "Ошибка" } });

const SERIES = [{ id: "s-1", isActive: true }];

/** The module keeps its catalogs between calls, so every case starts on a fresh copy. */
async function fresh() {
  vi.resetModules();
  const { useContestSeries } = await import("./useContestSeries");
  return useContestSeries();
}

beforeEach(() => {
  for (const method of Object.values(api)) method.mockReset();
  api.getAwardTypes.mockImplementation(() => listed([]));
  api.getContestSeries.mockImplementation(() => listed(SERIES));
  api.getModerationAwardTypes.mockImplementation(() => listed([]));
  api.getModerationContestSeries.mockImplementation(() => listed(SERIES));
});

describe("useContestSeries", () => {
  it("keeps the rows shown while a reload is read", async () => {
    const catalogs = await fresh();
    await catalogs.load();
    await catalogs.loadAdmin();

    let answer!: () => void;
    api.getModerationContestSeries.mockImplementation(
      () =>
        new Promise((resolve) => {
          answer = () => resolve({ data: { resources: [] }, error: null });
        }),
    );
    const reloading = catalogs.reload();
    await vi.waitFor(() => expect(answer).toBeTypeOf("function"));

    expect(catalogs.series.value).toEqual(SERIES);
    expect(catalogs.adminSeries.value).toEqual(SERIES);
    expect(catalogs.loading.value).toBe(false);

    answer();
    await reloading;
    expect(catalogs.adminSeries.value).toEqual([]);
  });

  it("tells a refused read from an empty catalog", async () => {
    api.getModerationContestSeries.mockImplementation(refused);
    const catalogs = await fresh();
    await catalogs.loadAdmin();

    expect(catalogs.adminFailed.value).toBe(true);
    expect(catalogs.adminSeries.value).toBeNull();

    api.getModerationContestSeries.mockImplementation(() => listed(SERIES));
    await catalogs.loadAdmin(true);
    expect(catalogs.adminFailed.value).toBe(false);
    expect(catalogs.adminSeries.value).toEqual(SERIES);
  });

  it("flags a refused public read and leaves its rows unset", async () => {
    api.getContestSeries.mockImplementation(refused);
    const catalogs = await fresh();
    await catalogs.load();

    expect(catalogs.failed.value).toBe(true);
    expect(catalogs.series.value).toBeNull();
  });
});
