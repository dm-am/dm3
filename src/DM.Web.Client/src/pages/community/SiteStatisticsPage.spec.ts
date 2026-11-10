/**
 * @vitest-environment jsdom
 */

// The banner is the page's only carrier of a failed load, so the read owns every refusal.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { statisticsApi } from "@/entities/statistics";
import SiteStatisticsPage from "./SiteStatisticsPage.vue";

vi.mock("@/entities/statistics", () => ({
  statisticsApi: { getLeaderboards: vi.fn() },
}));

const FAILED_TO_LOAD = "Не удалось загрузить статистику";

async function render(address = "/statistics") {
  const { wrapper } = await mountPage(SiteStatisticsPage, address, {
    routes: [["statistics", "/statistics"]],
    stubs: {
      // Rendered, not merely stubbed: the banner's text is the assertion.
      ErrorState: {
        props: ["message"],
        template: "<div class='error-state'>{{ message }}</div>",
      },
      LeadText: true,
      MonthYearPicker: true,
      SegmentedControl: true,
      StatBoard: true,
    },
  });
  await flushPromises();
  return wrapper;
}

describe("SiteStatisticsPage", () => {
  beforeEach(() => vi.mocked(statisticsApi.getLeaderboards).mockReset());

  it.each([0, 401, 403, 500, 503])(
    "keeps its own banner for a failure of status %i",
    async (status) => {
      vi.mocked(statisticsApi.getLeaderboards).mockResolvedValue({
        data: null,
        error: { status, title: "" },
      } as never);

      const wrapper = await render();

      expect(wrapper.get(".error-state").text()).toBe(FAILED_TO_LOAD);
    },
  );

  // Without the flags the interceptor would raise the banner's words on a plate as well.
  it("reads with the refusal taken over from the interceptor", async () => {
    vi.mocked(statisticsApi.getLeaderboards).mockResolvedValue({
      data: { resource: {} },
      error: null,
    } as never);

    await render();

    expect(vi.mocked(statisticsApi.getLeaderboards)).toHaveBeenCalledWith(
      expect.any(Number),
      expect.any(Number),
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });

  // Only the shared wording names the seconds; the 429 title carries no number.
  it("says how long the wait is when the limiter refuses the read", async () => {
    vi.mocked(statisticsApi.getLeaderboards).mockResolvedValue({
      data: null,
      error: {
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 12,
      },
    } as never);

    const wrapper = await render();

    expect(wrapper.get(".error-state").text()).toBe(
      "Слишком много попыток, попробуйте через 12 секунд",
    );
  });

  it("reads out what the server named instead of the fallback", async () => {
    vi.mocked(statisticsApi.getLeaderboards).mockResolvedValue({
      data: null,
      error: { status: 404, title: "Такого периода еще не было" },
    } as never);

    const wrapper = await render();

    expect(wrapper.get(".error-state").text()).toBe(
      "Такого периода еще не было",
    );
  });

  it("reads the period the address names", async () => {
    vi.mocked(statisticsApi.getLeaderboards).mockResolvedValue({
      data: { resource: {} },
      error: null,
    } as never);

    await render("/statistics?year=2024&month=3");

    expect(vi.mocked(statisticsApi.getLeaderboards)).toHaveBeenCalledWith(
      2024,
      3,
      expect.anything(),
    );
  });

  it("shows no banner over boards that loaded", async () => {
    vi.mocked(statisticsApi.getLeaderboards).mockResolvedValue({
      data: { resource: {} },
      error: null,
    } as never);

    const wrapper = await render();

    expect(wrapper.find(".error-state").exists()).toBe(false);
  });
});
