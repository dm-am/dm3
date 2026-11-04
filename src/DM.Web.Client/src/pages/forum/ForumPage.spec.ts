/**
 * @vitest-environment jsdom
 */

// "Форум" heads every forum page; on a board and its topics the strip of boards
// stands under it, the page's board first. The index has its table instead.
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { createMemoryHistory, createRouter, RouterView } from "vue-router";
import { useBoardsStore, type Board } from "@/entities/forum";
import ForumPage from "./ForumPage.vue";

const Empty = defineComponent({ render: () => null });

const board = (alias: string, title: string): Board =>
  ({ id: `id-${alias}`, alias, title, description: "" }) as unknown as Board;

const BOARDS = [board("general", "Общий"), board("ideas", "Котел идей")];

let mounted: VueWrapper | null = null;

async function forumAt(location: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: "/forum",
        component: ForumPage,
        children: [
          { path: "", name: "forum-index", component: Empty },
          { path: ":alias", name: "forum", component: Empty },
          { path: ":alias/:num", name: "topic", component: Empty },
        ],
      },
    ],
  });
  await router.push(location);
  await router.isReady();

  const store = useBoardsStore();
  store.boards = BOARDS;
  vi.spyOn(store, "fetchBoards").mockResolvedValue(undefined);
  vi.spyOn(store, "trySelectBoardByAlias").mockImplementation(
    async (alias: string) => {
      store.selectedBoard = BOARDS.find((b) => b.alias === alias) ?? null;
      return { ok: true, status: 200 } as never;
    },
  );

  const Root = defineComponent({ render: () => h(RouterView) });
  mounted = mount(Root, {
    global: {
      plugins: [router],
      stubs: { "page-title": { template: "<h1><slot /></h1>" } },
    },
  });
  await flushPromises();
  return mounted;
}

describe("the heading of the forum", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    mounted?.unmount();
    mounted = null;
    vi.restoreAllMocks();
  });

  for (const location of ["/forum/ideas", "/forum/ideas/5"]) {
    it(`says "Форум" with the strip of boards under it on ${location}`, async () => {
      const wrapper = await forumAt(location);

      const heading = wrapper.get("h1");
      expect(heading.text()).toBe("Форум");
      const strip = heading.element.nextElementSibling;
      expect(strip?.tagName).toBe("NAV");
      expect(strip?.getAttribute("aria-label")).toBe("Разделы форума");
      expect(strip?.querySelector("a.board-link.active")?.textContent).toBe(
        "Котел идей",
      );
      expect(wrapper.findAll("h1")).toHaveLength(1);
    });
  }

  it('says "Форум" with no strip on the index, where the table is', async () => {
    const wrapper = await forumAt("/forum");

    expect(wrapper.get("h1").text()).toBe("Форум");
    expect(wrapper.find("nav").exists()).toBe(false);
  });
});

// A board that would not open answers once: the error page of the status, so
// the read owns its refusal, a 429 included.
describe("the read that opens a board", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    mounted?.unmount();
    mounted = null;
    vi.restoreAllMocks();
  });

  for (const location of ["/forum/ideas", "/forum/ideas/5"]) {
    it(`takes the refusal over on ${location}`, async () => {
      await forumAt(location);

      expect(
        vi.mocked(useBoardsStore().trySelectBoardByAlias).mock.calls,
      ).toEqual([["ideas", { ownsRefusal: true, ownsRateLimit: true }]]);
    });
  }
});
