/**
 * @vitest-environment jsdom
 */

/**
 * One refusal, one carrier — and for a failed load the carrier is the page.
 *
 * The response interceptor raises a plate for a refused read, a rate limit, a
 * dead server, a dead connection and an expired session, and elsewhere a screen
 * that also prints such a failure in its own line is saying it twice. Not here:
 * this page has boards and nothing else. With the banner gone, a period the
 * server never answered for would be drawn as eight empty boards under "Данных
 * за период пока нет" — a statement about the period, made about a period
 * nobody was told about. So the banner speaks for every status, and it is the
 * read that has to take the refusal over.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { flushPromises, mount } from "@vue/test-utils";
import { statisticsApi } from "@/entities/statistics";
import { compileSass, ruleMap, sassBlocks } from "@/shared/lib/testing/styles";
import { MonthYearPicker } from "@/shared/ui/MonthYearPicker";
import SiteStatisticsPage from "./SiteStatisticsPage.vue";

// By name: the control is generic in the value it toggles, and a generic
// component is no selector the types of test-utils accept.
const SEGMENTS = { name: "SegmentedControl" };

const SFC = join(
  dirname(fileURLToPath(import.meta.url)),
  "SiteStatisticsPage.vue",
);

vi.mock("@/entities/statistics", () => ({
  statisticsApi: { getLeaderboards: vi.fn() },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ query: {} }),
  useRouter: () => ({ replace: vi.fn() }),
}));

const FAILED_TO_LOAD = "Не удалось загрузить статистику";

async function render() {
  const wrapper = mount(SiteStatisticsPage, {
    shallow: true,
    global: {
      stubs: {
        // Rendered, not merely stubbed: the banner's text is the assertion.
        ErrorState: {
          props: ["message"],
          template: "<div class='error-state'>{{ message }}</div>",
        },
        // Registered by the app layer, which no test mounts.
        PageTitle: true,
        SecondaryText: true,
      },
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

  /**
   * The other half of the same arrangement: the banner keeps the sentence only
   * because the read takes the refusal over. Without the flags the interceptor
   * puts the very same words on a plate over it, and no assertion about the
   * banner's text can see that — the transport is mocked out here.
   */
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

  /**
   * A 429 names the seconds the server asked for, and only the shared wording
   * carries them: the title sent with the refusal has no number in it, and the
   * number is the part a reader acts on — it is what says whether to wait or to
   * leave. The page took the wait over from the interceptor, so it owes the
   * reader the sentence the interceptor would have raised.
   */
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

  it("shows no banner over boards that loaded", async () => {
    vi.mocked(statisticsApi.getLeaderboards).mockResolvedValue({
      data: { resource: {} },
      error: null,
    } as never);

    const wrapper = await render();

    expect(wrapper.find(".error-state").exists()).toBe(false);
  });
});

/**
 * The period, the toggle and the picker, as it stood before 5 October 2026:
 * the segments "Месяц | Год | Все время" and the pill "‹ Июль 2026 ›" with
 * its arrows, one run of inline children, so it copies as one line: "Месяц
 * Год Все время ‹ Июль 2026 ›". It used to scroll sideways on a phone, where
 * the two do not fit side by side; no row of the site scrolls now (owner, 12
 * October 2026: "Верни как было, но без скролло-ебланизма"), so the pill goes
 * down a line of its own there, level with the segments over it.
 */
describe("SiteStatisticsPage period", () => {
  async function renderPeriod() {
    vi.mocked(statisticsApi.getLeaderboards).mockResolvedValue({
      data: { resource: {} },
      error: null,
    } as never);
    const wrapper = mount(SiteStatisticsPage, {
      shallow: true,
      global: { stubs: { PageTitle: true, SecondaryText: true } },
    });
    await flushPromises();
    return wrapper;
  }

  it("is a plain row, not one that scrolls", async () => {
    const wrapper = await renderPeriod();

    const row = wrapper.get(".controls");
    expect(row.element.tagName).toBe("DIV");
    expect(wrapper.find(".scroll-row").exists()).toBe(false);
    expect(wrapper.find("scroll-row-stub").exists()).toBe(false);
  });

  it("is one run of the segments and the pill with its arrows", async () => {
    const wrapper = await renderPeriod();

    const runs = wrapper.findAll(".controls > .period-line");
    expect(runs).toHaveLength(1);
    expect(runs[0].element.tagName).toBe("SPAN");
    expect(runs[0].findAll("segmented-control-stub")).toHaveLength(1);
    expect(runs[0].findAll("month-year-picker-stub")).toHaveLength(1);
    expect(runs[0].find(".copy-space").exists()).toBe(true);

    // The ordinary segments and the ordinary pill: no look of a narrow row.
    // Read off the attributes, not the props: neither part declares
    // `compact` or `narrow` any more, so a page that passed one again would
    // put it among the attributes, and props() would never list it.
    const segments = wrapper.getComponent(SEGMENTS);
    expect(segments.attributes()).not.toHaveProperty("compact");
    const picker = wrapper.getComponent(MonthYearPicker);
    expect(picker.props("stepper")).toBe(true);
    expect(picker.attributes()).not.toHaveProperty("narrow");
  });

  it("sends the pill down a line where the two do not fit, and never scrolls", () => {
    const css = compileSass(sassBlocks(SFC));
    const rules = ruleMap(css);

    // A line that may break: nothing holds it to one, nothing scrolls it.
    const run = rules.get(".period-line");
    expect(run?.["white-space"]).toBeUndefined();
    expect(run?.overflow).toBeUndefined();
    expect(run?.["overflow-x"]).toBeUndefined();
    expect(rules.get(".controls")?.["overflow-x"]).toBeUndefined();

    // The gap beside is the segments' margin on the right, the gap under is a
    // margin on the top of each part, and the run stands as far up: a part
    // that opens a line, the segments or the pill under them, stands at the
    // left edge of the column and not 8 in from it. Nothing is pulled out to
    // the left: the run's own box would then stand 8 past the column, which
    // e2e page-width names.
    expect(run?.["margin-top"]).toBe("-8px");
    expect(run?.margin).toBeUndefined();
    expect(run?.["margin-left"]).toBeUndefined();
    const parts = rules.get(".period-segments, .period-picker");
    expect(parts?.["margin-top"]).toBe("8px");
    expect(parts?.margin).toBeUndefined();
    expect(parts?.["margin-left"]).toBeUndefined();
    expect(rules.get(".period-segments")?.["margin-right"]).toBe("8px");
    expect(rules.get(".period-picker")).toBeUndefined();
    expect(css).not.toMatch(/@(?:media|container)[^{]*\{[^@]*period-/);
  });
});

/**
 * The boards of the page stand side by side by the width of their zone, not
 * of the window, and by the same rule as the boards in the digest topic of a
 * period: one board in the middle column at 1024, which is 285 wide between
 * two sidebars, three on a wide window. The rule and its widths are checked
 * once, in PeriodDigestBoards.spec.ts; here the page is held equal to it.
 */
describe("SiteStatisticsPage boards", () => {
  const DIGEST = join(
    dirname(SFC),
    "../../features/leaderboard/ui/PeriodDigestBoards.vue",
  );
  const columns = (file: string) => {
    const css = compileSass(sassBlocks(file));
    const grid = /\.boards-grid \{[^}]*\}/.exec(css)?.[0];
    const queries = [
      ...css.matchAll(/@container stat-boards [^{]*\{[^{}]*\{[^{}]*\}\s*\}/g),
    ].map(([query]) => query);
    return { css, grid, queries };
  };

  it("asks the zone of the boards, not the window", () => {
    const { css } = columns(SFC);
    expect(ruleMap(css).get(".boards-zone")?.container).toBe(
      "stat-boards/inline-size",
    );
    expect(css).not.toMatch(/@media[^{]*\{[^@]*boards-grid/);
  });

  it("stands its boards side by side as the digest topic does", () => {
    const page = columns(SFC);
    const digest = columns(DIGEST);
    expect(page.grid).toBeDefined();
    expect(page.queries).toHaveLength(2);
    expect(page.grid).toBe(digest.grid);
    expect(page.queries).toEqual(digest.queries);
  });
});
