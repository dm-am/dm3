/**
 * @vitest-environment jsdom
 */

/**
 * The game and the blog discussions had no search box, and the server was not
 * the reason: GameCommentsQuery, BlogCommentsQuery and the forum's CommentsQuery
 * declare the same fields, and those two pages simply sent a page number and
 * nothing else. So the first thing asked of the shared section is that the
 * whole filter standing in the URL reaches the loader — search, authors and
 * sort, everything the owner missed on those pages, is that one call.
 *
 * The rest is what the two lists did not have around them: paging above as
 * well as below, numbering that counts the discussion rather than the page,
 * a failed load that says so and can be retried, and an empty state that tells
 * "nothing written yet" apart from "nothing matches the filter".
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import type { Comment, PagingInfo } from "@/shared/api/models/common";
import DiscussionSection from "./DiscussionSection.vue";

/** The URL the section reads its filter, sort and page out of. */
const route = vi.hoisted(() => ({
  path: "/game/aaaaa/comments",
  name: "game-comments",
  params: { id: "aaaaa" } as Record<string, string>,
  query: {} as Record<string, string>,
  hash: "",
}));

vi.mock("vue-router", () => ({
  useRoute: () => route,
  useRouter: () => ({
    currentRoute: { value: route },
    replace: vi.fn(),
    resolve: () => ({ href: "" }),
  }),
  RouterLink: { template: "<a><slot /></a>" },
}));

// The filter bar is a dropdown forest of its own and has nothing to say here:
// the section reads the filter from the URL either way.
vi.mock("@/features/comment-filter/ui/CommentsFilter.vue", () => ({
  default: { template: "<div class='filter-stub' />" },
}));

// The item has its own spec; here only the number it is given matters.
vi.mock("@/features/comment", () => ({
  CommentItem: {
    props: ["comment", "number"],
    template: "<article class='comment-stub'>{{ number }}</article>",
  },
  useCommentWarnDialog: () => ({ warnComment: vi.fn() }),
}));

vi.mock("@/features/auth", () => ({
  LoginPrompt: { template: "<div class='login-prompt' />" },
}));

// A heavy tiptap component; what the composer owes a failed send is held by
// composerDrafts.spec.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    props: ["modelValue"],
    emits: ["update:modelValue", "submit"],
    template: "<textarea class='editor-stub' :value='modelValue' />",
  },
}));

const comment = (id: string): Comment =>
  ({
    id,
    text: "",
    likes: [],
    isRemoved: false,
    modifiedUtc: null,
  }) as unknown as Comment;

const paging = (over: Partial<PagingInfo> = {}): PagingInfo => ({
  pages: 3,
  current: 1,
  skip: 0,
  take: 20,
  total: 45,
  ...over,
});

function render(over: Record<string, unknown> = {}) {
  const load = vi.fn();
  const wrapper = mount(DiscussionSection, {
    global: {
      plugins: [createPinia()],
      stubs: { RouterLink: { template: "<a><slot /></a>" } },
    },
    props: {
      comments: [],
      paging: null,
      loading: false,
      failed: false,
      recordId: "aaaaa",
      load,
      create: vi.fn().mockResolvedValue({ error: null }),
      submitEdit: vi.fn().mockResolvedValue({ error: null }),
      submitDelete: vi.fn().mockResolvedValue({ error: null }),
      like: vi.fn(),
      unlike: vi.fn(),
      fetchEditSource: vi.fn(),
      pagingTo: { name: "game-comments", params: { id: "aaaaa" } },
      draftKey: "game:the-game:comment",
      canComment: false,
      ...over,
    },
  });
  return { wrapper, load };
}

describe("the discussion section", () => {
  beforeEach(() => {
    route.query = {};
    route.hash = "";
    localStorage.clear();
  });

  it("hands the loader the whole filter, not just the page", () => {
    route.query = {
      search: "правила",
      authors: "Мастер,Игрок",
      sortBy: "likes",
      sortOrder: "desc",
      number: "3",
    };

    const { load } = render();

    expect(load).toHaveBeenCalledWith({
      search: "правила",
      authors: ["Мастер", "Игрок"],
      sortBy: "likes",
      sortOrder: "desc",
      number: 3,
      size: 20,
    });
  });

  it("reloads when the record under it changes", async () => {
    const { wrapper, load } = render();
    expect(load).toHaveBeenCalledTimes(1);

    await wrapper.setProps({ recordId: "bbbbb" });

    expect(load).toHaveBeenCalledTimes(2);
  });

  it("pages above the list as well as below it", () => {
    const { wrapper } = render({
      comments: [comment("c-1")],
      paging: paging(),
    });

    expect(wrapper.findAll(".paging-block")).toHaveLength(2);
  });

  it("numbers a comment by its place in the discussion, not on the page", () => {
    const { wrapper } = render({
      comments: [comment("c-21"), comment("c-22")],
      paging: paging({ current: 2, skip: 20 }),
    });

    expect(wrapper.findAll(".comment-stub").map((item) => item.text())).toEqual(
      ["21", "22"],
    );
  });

  it("says a load failed and offers the retry", async () => {
    const { wrapper, load } = render({ failed: true });

    const failure = wrapper.find(".error-state");
    expect(failure.text()).toContain("Не удалось загрузить комментарии");

    await failure.find("button").trigger("click");

    expect(load).toHaveBeenCalledTimes(2);
  });

  it("tells an empty discussion apart from an empty filter result", () => {
    expect(render().wrapper.find(".discussion-none").text()).toBe(
      "Комментариев пока нет",
    );

    route.query = { search: "такого нет" };

    expect(render().wrapper.find(".discussion-none").text()).toBe(
      "Комментариев по заданным фильтрам не найдено",
    );
  });
});
