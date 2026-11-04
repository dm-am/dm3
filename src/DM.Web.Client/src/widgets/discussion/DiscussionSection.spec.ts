/**
 * @vitest-environment jsdom
 */

/** The discussion section: the whole filter reaches the loader, paging both ends, its states. */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
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
}));

// The section reads the filter from the URL; the bar has nothing to say here.
vi.mock("@/features/comment-filter/ui/CommentsFilter.vue", () => ({
  default: { template: "<div class='filter-stub' />" },
}));

// The item has its own spec: here only what it is given matters.
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

vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeComposer(
    () => {},
  ),
);

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
      stubs: { RouterLink: RouterLinkStub },
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

  // A 400 is the page's to show: the reason that names what is wrong with the text.
  it("names the server's reason and keeps the text when a send is refused", async () => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
    const reason =
      "Скрытый блок работает только в игровых постах: [private]. Для примера оберните его в [noparse].";
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

  /** The blacklist closes the composer and reaches every comment, even when it arrives late. */
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

  // A ban: no block at all, so no empty step under the list.
  it("leaves no empty block where there is nothing to offer and nothing to say", () => {
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    const { wrapper } = render({ comments: [comment("c-1")] });

    expect(wrapper.find(".discussion-composer").exists()).toBe(false);
  });
});

/** A discussion the viewer may read and not write in stays whole, a plaque in the composer's place. */
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

  // Signing in would not open the form.
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

  // Each drawn by its own component, so a refusal set in grey would read as nothing.
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
});
