/**
 * @vitest-environment jsdom
 */

// The last activity of a topic on the Topics tab, dated as the forum's table
// dates it: by its last comment, or by its opening when nobody answered.
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { useAuthStore } from "@/entities/user";
import ProfileTopicsList from "./ProfileTopicsList.vue";

const { mockGetAllTopics } = vi.hoisted(() => ({
  mockGetAllTopics: vi.fn(),
}));

vi.mock("@/entities/forum/api/forumApi", () => ({
  default: {
    getAllTopics: mockGetAllTopics,
    getTopics: vi.fn(),
    getBoards: vi.fn(),
    getNews: vi.fn(),
  },
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

/** The tab at the address of the profile, narrowed by `query` as a reader's filter would. */
async function mountList(query: Record<string, string> = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const search = new URLSearchParams(query).toString();
  const address = search ? `/users/author?${search}` : "/users/author";
  const { wrapper } = await mountPage(ProfileTopicsList, address, {
    routes: [
      ["profile", "/users/:username"],
      ["forum", "/forum/:alias"],
      ["topic", "/forum/:alias/:number"],
    ],
    pinia,
    props: { username: "author" },
    stubs: {
      Tooltip: { template: `<div><slot /></div>` },
      TopicsFilter: true,
      ErrorState: true,
      Paging: true,
      SvgIcon: true,
      HumanDate: true,
      UserLink: true,
      RemovedAccountLink: true,
    },
  });
  mounted = wrapper;
  await flushPromises();
  return wrapper;
}

/** The last-activity cell of the row of the topic titled `title`. */
const cell = (wrapper: VueWrapper, title: string) => {
  const row = wrapper
    .findAll("tr.table-row")
    .find((one) => one.get("td.col-title").text() === title);
  if (!row) throw new Error(`no row ${title}`);
  return row.get("td.col-lastActivity");
};

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

    const quiet = cell(wrapper, QUIET.title);
    expect(quiet.text()).not.toContain(VALUE_UNAVAILABLE);
    expect(quiet.get("human-date-stub").attributes("date")).toBe(
      QUIET.createdUtc,
    );
  });

  it("keeps dating an answered topic by its last comment", async () => {
    const wrapper = await mountList();

    expect(
      cell(wrapper, ANSWERED.title).get("human-date-stub").attributes("date"),
    ).toBe(ANSWERED.lastComment.createdUtc);
  });
  it("leads the board cell to the board of the topic", async () => {
    const wrapper = await mountList();

    const link = wrapper.get("tr.table-row td.col-board a.board-link");
    expect(link.attributes("href")).toBe("/forum/main");
  });

  // Who was last stands before the date, as in the forum's table of topics.
  it("names who wrote last, and who opened a topic nobody answered", async () => {
    const wrapper = await mountList();

    const answered = cell(wrapper, ANSWERED.title).findComponent({
      name: "UserLink",
    });
    expect(answered.exists()).toBe(true);
    expect(answered.props("user")).toEqual(ANSWERED.lastComment.author);
    const quiet = cell(wrapper, QUIET.title).findComponent({
      name: "UserLink",
    });
    expect(quiet.props("user")).toEqual(QUIET.author);
  });
});

/** Every column of the tab stands, and the header of the sorted one announces the sort (aria-sort). */
describe("ProfileTopicsList sorted column", () => {
  const EVERY = [
    "Топик",
    "Раздел",
    "Комментарии",
    "Нравится",
    "Дата создания",
    "Последняя активность",
  ];
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

  /** The tab, its table drawn for real, sorted as `query` says. */
  const sortedBy = (query: Record<string, string> = {}) => mountList(query);

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

/** The "Комментарии" cell of a signed-in reader: the count and the unread one beside it, "(0)" included. */
describe("ProfileTopicsList comments", () => {
  const UNREAD = {
    ...ANSWERED,
    id: "t-unread",
    topicNumber: 4,
    title: "Тема 4",
    commentsCount: 45,
    unreadCommentsCount: 3,
    lastComment: { ...ANSWERED.lastComment, id: "c-45" },
  };
  const READ = { ...ANSWERED, commentsCount: 12 };

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAllTopics.mockResolvedValue({
      data: {
        resources: [READ, UNREAD],
        paging: { current: 1, skip: 0, take: 20, total: 2, pages: 1 },
      },
      error: null,
    });
  });

  async function mountSignedIn() {
    const wrapper = await mountList();
    useAuthStore().updateUser({ username: "reader" } as never);
    await flushPromises();
    return wrapper;
  }

  const comments = (wrapper: VueWrapper, title: string) => {
    const row = wrapper
      .findAll("tr.table-row")
      .find((one) => one.get("td.col-title").text() === title);
    if (!row) throw new Error(`no row ${title}`);
    return row.get("td.col-comments");
  };

  it("writes (0) beside a topic read through", async () => {
    const wrapper = await mountSignedIn();

    expect(comments(wrapper, READ.title).text()).toBe("12 (0)");
  });

  it("links the unread count to the topic's first page, at the newest comment", async () => {
    const wrapper = await mountSignedIn();

    const cell = comments(wrapper, UNREAD.title);
    expect(cell.text()).toBe("45 (3)");
    expect(cell.get("a.unread").attributes("href")).toBe(
      "/forum/main/4#comment-c-45",
    );
  });
});
