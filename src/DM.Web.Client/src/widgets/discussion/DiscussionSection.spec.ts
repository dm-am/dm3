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
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import type { Comment, PagingInfo } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
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

// The item has its own spec; here only what it is given matters: the number,
// and whether the reader is on the blacklist of the record. The form is the
// real one (CommentComposer): what the section owes a send is what it does.
vi.mock("@/features/comment", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/features/comment")>()),
  CommentItem: {
    props: ["comment", "number", "viewerBlacklisted"],
    template:
      "<article class='comment-stub' :data-blacklisted='String(!!viewerBlacklisted)'>{{ number }}</article>",
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
    template:
      "<textarea class='editor-stub' :value='modelValue' @input='$emit(\"update:modelValue\", $event.target.value)' />",
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

  // The interceptor leaves a 400 to the page, and that is the refusal that
  // names what is wrong with the text. The section handed the text back and
  // said nothing, so the reader saw it return and learnt nothing.
  it("names the server's reason and keeps the text when a send is refused", async () => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
    const reason = "Скрытый текст работает только в игровом тексте поста";
    const create = vi.fn().mockResolvedValue({
      error: {
        type: "",
        title: "",
        status: 400,
        traceId: "t",
        errors: { Text: [reason] },
      },
    });
    const { wrapper } = render({ canComment: true, create });

    await wrapper.get(".editor-stub").setValue("[private]секрет[/private]");
    await wrapper.get(".discussion-composer button").trigger("click");
    await flushPromises();

    expect(create).toHaveBeenCalledWith("[private]секрет[/private]");
    // Under the "Отправить" that sent it, and in no plate.
    expect(wrapper.get(".comment-composer__refusal").text()).toBe(reason);
    expect(toasts.value.map((t) => t.message)).toEqual([]);
    expect(
      (wrapper.get(".editor-stub").element as HTMLTextAreaElement).value,
    ).toBe("[private]секрет[/private]");
  });

  /**
   * The blacklist of the game or blog closes the composer (the page says so
   * through canComment and closedHint) and reaches every comment, where it
   * takes "Редактировать" off the viewer's own. The list is memoised per
   * item, so a flag that changes after the first page has rendered - the game
   * answering after the comments - has to be one of the things the memo
   * watches, or the comments keep offering what the server will refuse.
   */
  it("says why there is no composer and tells every comment about the blacklist", async () => {
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    const { wrapper } = render({
      comments: [comment("c-1"), comment("c-2")],
      canComment: true,
    });
    const flags = () =>
      wrapper
        .findAll(".comment-stub")
        .map((item) => item.attributes("data-blacklisted"));
    expect(flags()).toEqual(["false", "false"]);

    await wrapper.setProps({
      canComment: false,
      closedHint: "Вы в черном списке этой игры: писать в обсуждении нельзя",
      viewerBlacklisted: true,
    });

    expect(flags()).toEqual(["true", "true"]);
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.get(".discussion-hint").text()).toBe(
      "Вы в черном списке этой игры: писать в обсуждении нельзя",
    );
  });

  // A signed-in reader the record takes no comment from and says nothing
  // about (a ban): no block at all, so no empty step under the list.
  it("leaves no empty block where there is nothing to offer and nothing to say", () => {
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    const { wrapper } = render({ comments: [comment("c-1")] });

    expect(wrapper.find(".discussion-composer").exists()).toBe(false);
  });
});

/**
 * A discussion the viewer may read and not write in: a game read-only to
 * those outside it, the blacklist of the game or the blog. It stays whole,
 * empty or not, and the plaque of a refusal (RefusalNote) stands where the
 * composer would be. The plaque is what tells it from the empty line,
 * "Комментариев пока нет", which stays grey: the two never read alike.
 */
describe("a discussion closed to writing", () => {
  const OFF = "Писать в обсуждении могут только участники игры";

  beforeEach(() => {
    route.query = {};
    route.hash = "";
    localStorage.clear();
  });

  it("says so under the comments, with no form", async () => {
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    const { wrapper } = render({
      closedHint: OFF,
      comments: [comment("c-1")],
      paging: paging({ total: 1, pages: 1 }),
    });
    await flushPromises();

    expect(wrapper.findAll(".comment-stub")).toHaveLength(1);
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.get(".discussion-composer .discussion-hint").text()).toBe(
      OFF,
    );
  });

  // Signing in would not open the form, so the guest is told why there is
  // none rather than invited to sign in.
  it("tells a guest the same instead of inviting him in", async () => {
    const { wrapper } = render({ closedHint: OFF });
    await flushPromises();

    expect(wrapper.get(".discussion-hint").text()).toBe(OFF);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
  });

  it("invites a guest in where signing in opens the composer", async () => {
    const { wrapper } = render();
    await flushPromises();

    expect(wrapper.find(".login-prompt").exists()).toBe(true);
    expect(wrapper.find(".discussion-hint").exists()).toBe(false);
  });

  // The discussion with nothing in it used to vanish, filter bar and all, and
  // the page left its address: there is something to read in it after all,
  // "Комментариев пока нет", and the line under it.
  it("keeps an empty one whole: the filter bar, the empty line and the line under it", async () => {
    const { wrapper } = render({
      closedHint: OFF,
      paging: paging({ total: 0, pages: 0 }),
    });
    await flushPromises();

    expect(wrapper.find(".filter-stub").exists()).toBe(true);
    expect(wrapper.get(".discussion-none").text()).toBe(
      "Комментариев пока нет",
    );
    expect(wrapper.get(".discussion-hint").text()).toBe(OFF);
  });

  // The refusal is the plaque and the empty line is grey, each drawn by its
  // own component, so a refusal set back in grey reads as nothing here.
  it("draws the refusal as a plaque and the empty line in grey", async () => {
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    const { wrapper } = render({
      closedHint: OFF,
      paging: paging({ total: 0, pages: 0 }),
    });
    await flushPromises();

    const plaques = wrapper.findAllComponents(RefusalNote);
    expect(plaques.map((plaque) => plaque.text())).toEqual([OFF]);
    expect(plaques[0].classes()).toContain("discussion-hint");
    const grey = wrapper.findAllComponents(SecondaryText);
    expect(grey.map((line) => line.text())).toEqual(["Комментариев пока нет"]);
  });

  it("keeps an open empty discussion and its words", async () => {
    const { wrapper } = render({ paging: paging({ total: 0, pages: 0 }) });
    await flushPromises();

    expect(wrapper.find(".discussion-none").text()).toBe(
      "Комментариев пока нет",
    );
  });
});
