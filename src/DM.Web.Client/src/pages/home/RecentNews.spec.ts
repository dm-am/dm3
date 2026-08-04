/**
 * @vitest-environment jsdom
 */

/**
 * The block used to show every news topic younger than seven days, so its card
 * count — and its height — moved with the calendar. The owner's rule is at most
 * two at a time.
 *
 * The cap is asserted against a response that ignores it: the request already
 * carries `take` (forumApi.spec), and this pins the other half, that the block
 * itself never renders a third card whatever comes back. The empty-window
 * fallback is asserted too, because a naive slice would have dropped it.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { NEWS_WIDGET_LIMIT } from "@/entities/forum";
import RecentNews from "./RecentNews.vue";

const { mockGetNews } = vi.hoisted(() => ({ mockGetNews: vi.fn() }));

// Only the transport is replaced: NEWS_WIDGET_LIMIT stays the real constant, so
// raising it in one of its two uses and not the other still fails somewhere.
vi.mock("@/entities/forum/api/forumApi", async (importOriginal) => {
  const actual = (await importOriginal()) as Record<string, unknown>;
  return { ...actual, default: { getNews: mockGetNews } };
});

const hoursAgo = (hours: number) =>
  new Date(Date.now() - hours * 3600_000).toISOString();

const topic = (id: string, createdUtc: string) => ({
  id,
  topicNumber: 1,
  title: `Новость ${id}`,
  description: "Текст новости",
  author: { username: "author", role: "RegularUser", lastActivityUtc: null },
  createdUtc,
  modifiedUtc: null,
  isAttached: false,
  isClosed: false,
  commentsCount: 0,
  unreadCommentsCount: 0,
  likes: [],
  likesCount: 0,
  lastActivityUtc: createdUtc,
  lastComment: null,
  board: { id: "b-news", alias: "news", title: "Новости" },
});

async function mountBlock() {
  const pinia = createPinia();
  setActivePinia(pinia);

  const wrapper = mount(RecentNews, {
    shallow: true,
    global: {
      plugins: [pinia],
      stubs: {
        RouterLink: true,
        TopicView: { template: `<article class="news-card-stub" />` },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

describe("RecentNews", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows no more cards than the limit even when the server sends more", async () => {
    mockGetNews.mockResolvedValue({
      data: {
        resources: [
          topic("1", hoursAgo(1)),
          topic("2", hoursAgo(2)),
          topic("3", hoursAgo(3)),
          topic("4", hoursAgo(4)),
          topic("5", hoursAgo(5)),
        ],
      },
      error: null,
    });

    const wrapper = await mountBlock();

    expect(wrapper.findAll(".news-card-stub")).toHaveLength(NEWS_WIDGET_LIMIT);
  });

  it("falls back to the single latest topic when nothing is fresh", async () => {
    mockGetNews.mockResolvedValue({
      data: {
        resources: [
          topic("old-1", hoursAgo(24 * 30)),
          topic("old-2", hoursAgo(24 * 60)),
        ],
      },
      error: null,
    });

    const wrapper = await mountBlock();

    expect(wrapper.findAll(".news-card-stub")).toHaveLength(1);
  });
});
