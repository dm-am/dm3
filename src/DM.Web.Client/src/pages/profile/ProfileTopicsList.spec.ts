/**
 * @vitest-environment jsdom
 */

/**
 * The last activity of a topic on the profile's Topics tab is dated the way the
 * forum's own table of topics dates it: by its last comment, and a topic
 * nobody has answered by its opening. It used to print the missing-value token
 * there, which the forum table stopped doing.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import ProfileTopicsList from "./ProfileTopicsList.vue";

const { mockGetAllTopics, routeQuery } = vi.hoisted(() => ({
  mockGetAllTopics: vi.fn(),
  /** The query of the address; empty but where a test sorts the listing. */
  routeQuery: {} as Record<string, string>,
}));

vi.mock("@/entities/forum/api/forumApi", () => ({
  default: {
    getAllTopics: mockGetAllTopics,
    getTopics: vi.fn(),
    getBoards: vi.fn(),
    getNews: vi.fn(),
  },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { username: "author" }, query: routeQuery }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

const topic = (id: string, number: number) => ({
  id,
  topicNumber: number,
  title: `Тема ${number}`,
  isAttached: false,
  isClosed: false,
  commentsCount: 0,
  unreadCommentsCount: 0,
  likesCount: 0,
  createdUtc: "2026-07-01T10:00:00Z",
  lastComment: null,
  author: { username: "author", role: "RegularUser" },
  board: { id: "b-1", alias: "main", title: "Общий" },
});

const QUIET = topic("t-quiet", 2);
const ANSWERED = {
  ...topic("t-answered", 3),
  commentsCount: 1,
  lastComment: {
    id: "c-1",
    createdUtc: "2026-07-05T12:00:00Z",
    author: { username: "reader", role: "RegularUser" },
  },
};

let mounted: VueWrapper | null = null;

afterEach(() => {
  mounted?.unmount();
  mounted = null;
});

/** `stubs` replaces the named stubs below for one test. */
async function mountList(stubs: Record<string, unknown> = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const wrapper = mount(ProfileTopicsList, {
    props: { username: "author" },
    shallow: true,
    global: {
      plugins: [pinia],
      stubs: {
        RouterLink: { props: ["to"], template: `<a :href="to"><slot /></a>` },
        Tooltip: { template: `<div><slot /></div>` },
        DataTable: {
          props: ["data", "columns"],
          template: `<table><tbody><tr v-for="row in data" :key="row.id" class="row" :data-id="row.id"><td v-for="column in columns" :key="column.key" :class="'col-' + column.key"><slot :name="'cell-' + column.key" :row="row" /></td></tr></tbody></table>`,
        },
        ...stubs,
      },
    },
  });
  mounted = wrapper;
  await flushPromises();
  return wrapper;
}

const cell = (wrapper: VueWrapper, id: string) =>
  wrapper.get(`tr.row[data-id="${id}"] td.col-lastActivity`);

describe("ProfileTopicsList last activity", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAllTopics.mockResolvedValue({
      data: {
        resources: [QUIET, ANSWERED],
        paging: { current: 1, skip: 0, take: 20, total: 2, pages: 1 },
      },
      error: null,
    });
  });

  it("dates a topic nobody has answered by its opening", async () => {
    const wrapper = await mountList();

    const quiet = cell(wrapper, "t-quiet");
    expect(quiet.text()).not.toContain(VALUE_UNAVAILABLE);
    expect(quiet.get("human-date-stub").attributes("date")).toBe(
      QUIET.createdUtc,
    );
  });

  it("keeps dating an answered topic by its last comment", async () => {
    const wrapper = await mountList();

    expect(
      cell(wrapper, "t-answered").get("human-date-stub").attributes("date"),
    ).toBe(ANSWERED.lastComment.createdUtc);
  });
  // Who was last stands before the date, as in the forum table of topics:
  // under the pointer only, it could not be read on a touch screen.
  it("names who wrote last, and who opened a topic nobody answered", async () => {
    const wrapper = await mountList();

    const answered = cell(wrapper, "t-answered").findComponent({
      name: "UserLink",
    });
    expect(answered.exists()).toBe(true);
    expect(answered.props("user")).toEqual(ANSWERED.lastComment.author);
    const quiet = cell(wrapper, "t-quiet").findComponent({ name: "UserLink" });
    expect(quiet.props("user")).toEqual(QUIET.author);
  });
});

/**
 * Every column of the tab stands in a box narrower than its words, as on a
 * board (TopicsList), the one it is sorted by among them, and the header of
 * that one announces the sort (aria-sort).
 *
 * jsdom lays nothing out: the box is answered by `ROOM`, a header by the least
 * width of its column, both as a phone of 360 read them off the topics of
 * SolohinLex in the seed, so a table that dropped columns to fit would drop
 * them here.
 */
describe("ProfileTopicsList sorted column in a narrow box", () => {
  /** The frame of the table on a phone of 360. */
  const ROOM = 296;
  /** Least widths of the columns, their words whole, at 360. */
  const LEAST: Record<string, number> = {
    title: 117.8,
    board: 91.5,
    comments: 120.7,
    likes: 67.2,
    created: 100.6,
    lastActivity: 173.6,
  };
  const EVERY = [
    "Топик",
    "Раздел",
    "Комментарии",
    "Лайки",
    "Дата создания",
    "Последняя активность",
  ];
  const realRect = Element.prototype.getBoundingClientRect;

  /** The key of a header of the table ("col-likes"), or null. */
  const keyOf = (cell: Element) =>
    [...cell.classList]
      .find((one) => one.startsWith("col-"))
      ?.slice("col-".length) ?? null;

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAllTopics.mockResolvedValue({
      data: {
        resources: [QUIET, ANSWERED],
        paging: { current: 1, skip: 0, take: 20, total: 2, pages: 1 },
      },
      error: null,
    });
    vi.stubGlobal(
      "ResizeObserver",
      vi.fn(function () {
        return { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
      }),
    );
    Element.prototype.getBoundingClientRect = function (this: Element) {
      const key = keyOf(this);
      const width = this.classList.contains("data-table-scroll")
        ? ROOM
        : this.tagName === "TH" && key
          ? (LEAST[key] ?? 0)
          : 0;
      return {
        width,
        height: 0,
        top: 0,
        left: 0,
        right: width,
        bottom: 0,
      } as DOMRect;
    };
  });

  afterEach(() => {
    Element.prototype.getBoundingClientRect = realRect;
    vi.unstubAllGlobals();
    for (const key of Object.keys(routeQuery))
      Reflect.deleteProperty(routeQuery, key);
  });

  /** The tab, its table drawn for real, sorted as `query` says. */
  async function sortedBy(query: Record<string, string> = {}) {
    Object.assign(routeQuery, query);
    return mountList({ DataTable: false });
  }

  /** The labels of the headers of the table. */
  const standing = (wrapper: VueWrapper) =>
    wrapper.findAll("thead th").map((th) => th.text());

  it("stands every column, sorted by the last activity by default", async () => {
    const wrapper = await sortedBy();

    expect(standing(wrapper)).toEqual(EVERY);
    expect(
      wrapper.get("thead th.col-lastActivity").attributes("aria-sort"),
    ).toBe("descending");
  });

  it("stands every column sorted by the date of creation", async () => {
    const wrapper = await sortedBy({ sortBy: "created", sortOrder: "asc" });

    expect(standing(wrapper)).toEqual(EVERY);
    expect(wrapper.get("thead th.col-created").attributes("aria-sort")).toBe(
      "ascending",
    );
  });
});
