/**
 * @vitest-environment jsdom
 */

/**
 * Two moderator actions on a board listing used to end in nothing. Pinning
 * reported neither outcome, and the reorder dialog closed on a rejected save —
 * which reads as success while the server keeps the old order.
 *
 * These pin the opposite, in the shape TopicPage already uses for closing and
 * deleting a topic: the refusal is spoken once through notifyFailure, and the
 * dialog closes only when the save landed.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { useBoardsStore } from "@/entities/forum";
import TopicsList from "./TopicsList.vue";

const { mockGetTopics, mockUpdateTopic, mockReorderPinnedTopics } = vi.hoisted(
  () => ({
    mockGetTopics: vi.fn(),
    mockUpdateTopic: vi.fn(),
    mockReorderPinnedTopics: vi.fn(),
  }),
);

vi.mock("@/entities/forum/api/forumApi", () => ({
  default: {
    getTopics: mockGetTopics,
    updateTopic: mockUpdateTopic,
    reorderPinnedTopics: mockReorderPinnedTopics,
    getBoards: vi.fn(),
    getNews: vi.fn(),
  },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { alias: "main" }, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

/** A status the response interceptor stays quiet about, so the page must speak. */
const refusal = (status: number): GeneralError => ({
  type: "",
  title: "",
  status,
  traceId: "trace",
});

const board = {
  id: "b-1",
  alias: "main",
  title: "Общий",
  moderators: [{ username: "moder" }],
};

const topic = (id: string, title: string, isAttached: boolean) => ({
  id,
  topicNumber: isAttached ? 1 : 2,
  title,
  isAttached,
  isClosed: false,
  commentsCount: 0,
  unreadCommentsCount: 0,
  likesCount: 0,
  createdUtc: "2026-07-01T10:00:00Z",
  lastComment: null,
  author: { username: "author", role: "RegularUser" },
});

const messages = () => useToast().toasts.value.map((t) => t.message);

async function mountList() {
  const pinia = createPinia();
  setActivePinia(pinia);
  // Set before mount: the listing's immediate watcher fetches for the board
  // the store already holds.
  useBoardsStore().selectedBoard = board as never;

  const wrapper = mount(TopicsList, {
    shallow: true,
    global: {
      plugins: [pinia],
      stubs: {
        RouterLink: true,
        // Both stubs keep the slot the assertions reach through: the pin button
        // lives in the table's per-row actions slot, and the reorder dialog is
        // observable only as "still mounted".
        Tooltip: { template: `<div><slot /></div>` },
        DataTable: {
          props: ["data"],
          template: `<div><div v-for="row in data" :key="row.id" class="row"><slot name="cell-actions" :row="row" /></div></div>`,
        },
        PinnedTopicsManager: {
          template: `<div class="pinned-stub"><button class="pinned-save" @click="$emit('save', ['t-pinned'])">Сохранить порядок</button></div>`,
        },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

describe("TopicsList moderator actions", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));

    localStorage.clear();
    // A board moderator: the actions column and the reorder button are his.
    localStorage.setItem(
      "user",
      JSON.stringify({ username: "moder", role: "RegularUser" }),
    );

    vi.clearAllMocks();
    mockGetTopics.mockImplementation(
      (_alias: string, query?: { isAttached?: boolean }) =>
        Promise.resolve(
          query?.isAttached
            ? {
                data: {
                  resources: [topic("t-pinned", "Правила", true)],
                  paging: null,
                },
                error: null,
              }
            : {
                data: {
                  resources: [topic("t-plain", "Болталка", false)],
                  paging: { number: 1, size: 20, total: 1, pages: 1 },
                },
                error: null,
              },
        ),
    );
    mockUpdateTopic.mockResolvedValue({ data: null, error: null });
    mockReorderPinnedTopics.mockResolvedValue({ data: null, error: null });
  });

  it("names the pin action the server refused, in the direction it was going", async () => {
    mockUpdateTopic.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await mountList();

    // First row is the pinned one, second the regular one.
    await wrapper.findAll(".pin-button")[0].trigger("click");
    await flushPromises();
    expect(messages()).toEqual(["Не удалось открепить топик"]);

    await wrapper.findAll(".pin-button")[1].trigger("click");
    await flushPromises();
    expect(messages()).toEqual([
      "Не удалось открепить топик",
      "Не удалось закрепить топик",
    ]);
  });

  it("keeps the reorder dialog open when the save was refused", async () => {
    mockReorderPinnedTopics.mockResolvedValue({
      data: null,
      error: refusal(409),
    });
    const wrapper = await mountList();

    await wrapper.find(".manage-pinned-button").trigger("click");
    expect(wrapper.find(".pinned-stub").exists()).toBe(true);

    await wrapper.find(".pinned-save").trigger("click");
    await flushPromises();

    expect(messages()).toEqual([
      "Не удалось сохранить порядок закрепленных топиков",
    ]);
    // Closing here would show the new order on screen and the old one on the
    // server, with nothing to tell them apart.
    expect(wrapper.find(".pinned-stub").exists()).toBe(true);
  });

  it("closes the reorder dialog only once the save landed", async () => {
    const wrapper = await mountList();

    await wrapper.find(".manage-pinned-button").trigger("click");
    await wrapper.find(".pinned-save").trigger("click");
    await flushPromises();

    expect(messages()).toEqual([]);
    expect(wrapper.find(".pinned-stub").exists()).toBe(false);
  });
});
