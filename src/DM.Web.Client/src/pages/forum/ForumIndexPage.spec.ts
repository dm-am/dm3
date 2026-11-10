/**
 * @vitest-environment jsdom
 */

// The forum index: "Непрочитанных: N | Отметить прочитанными" under the
// heading while some comment is unread (shared/ui/UnreadLine holds the line).
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { forumApi, useBoardsStore } from "@/entities/forum";
import { mountPage } from "@/shared/lib/testing/mountPage";
import ForumIndexPage from "./ForumIndexPage.vue";

const board = (id: string, unreadCommentsCount: number) => ({
  id,
  title: `Раздел ${id}`,
  alias: id,
  description: "",
  moderators: [],
  topicsCount: 3,
  commentsCount: 20,
  unreadTopicsCount: unreadCommentsCount ? 1 : 0,
  unreadCommentsCount,
  lastComment: null,
  lastTopic: null,
});

let mounted: VueWrapper | null = null;

afterEach(() => {
  mounted?.unmount();
  mounted = null;
  localStorage.clear();
});

/** The index under the forum shell's heading, the boards in the store. */
async function render(unread: number[], signedIn = true) {
  if (signedIn) {
    localStorage.setItem(
      "user",
      JSON.stringify({ username: "reader", role: "RegularUser" }),
    );
  }
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useBoardsStore();
  vi.spyOn(store, "fetchBoards").mockResolvedValue(undefined as never);
  store.boards = unread.map((n, i) => board(`b${i}`, n)) as never;
  const markForumAsRead = vi
    .spyOn(forumApi, "markForumAsRead")
    .mockResolvedValue({ data: null, error: null } as never);

  // The heading stands in the forum shell, above the index.
  const Shell = defineComponent({
    setup: () => () => h("main", [h("h1", "Форум"), h(ForumIndexPage)]),
  });
  const { wrapper } = await mountPage(Shell, "/forum", {
    routes: [
      ["forum-index", "/forum"],
      ["forum", "/forum/:alias"],
      ["topic", "/forum/:alias/:num"],
    ],
    pinia,
    attachTo: document.body,
    stubs: { DataTable: true, Tooltip: true },
  });
  mounted = wrapper;
  await flushPromises();
  return { wrapper, store, markForumAsRead };
}

/** The line under the heading as a reader sees it. */
function unreadLine(wrapper: VueWrapper): string | null {
  const line = wrapper.find(".unread-line");
  if (!line.exists()) return null;
  return line.element.textContent!.replace(/\s+/g, " ").trim();
}

describe("marking the forum read", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("offers nothing to mark when nothing is unread", async () => {
    const { wrapper } = await render([0, 0]);

    expect(wrapper.find("button").exists()).toBe(false);
    expect(wrapper.find(".unread-line").exists()).toBe(false);
  });

  it("counts the unread comments of every board under the heading", async () => {
    const { wrapper } = await render([3, 0, 12]);

    expect(unreadLine(wrapper)).toBe(
      "Непрочитанных: 15 | Отметить прочитанными",
    );
  });

  it("has nothing to mark for a guest, whom the server answers with totals", async () => {
    const { wrapper } = await render([3, 12], false);

    expect(wrapper.find(".unread-line").exists()).toBe(false);
  });

  it("marks the whole forum at one press, and the line goes", async () => {
    const { wrapper, store, markForumAsRead } = await render([3, 12]);

    await wrapper.get(".unread-mark").trigger("click");
    await flushPromises();

    expect(markForumAsRead).toHaveBeenCalledTimes(1);
    expect(store.boards!.map((b) => b.unreadCommentsCount)).toEqual([0, 0]);
    expect(wrapper.find(".unread-line").exists()).toBe(false);
    expect(document.activeElement).toBe(wrapper.get("h1").element);
    expect(wrapper.get("[role=status]").text()).toBe("Все топики прочитаны");
  });

  it("says a refusal under the line and keeps the count", async () => {
    const { wrapper, markForumAsRead } = await render([3, 12]);
    markForumAsRead.mockResolvedValue({
      data: null,
      error: { type: "about:blank", title: "", status: 404, traceId: "t" },
    } as never);

    await wrapper.get(".unread-mark").trigger("click");
    await flushPromises();

    expect(unreadLine(wrapper)).toBe(
      "Непрочитанных: 15 | Отметить прочитанными",
    );
    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось отметить топики прочитанными",
    );
  });
});
