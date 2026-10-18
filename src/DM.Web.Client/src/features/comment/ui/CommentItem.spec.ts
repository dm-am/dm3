/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import {
  enableAutoUnmount,
  mount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia } from "pinia";
import { useToast } from "@/shared/lib/composables/useToast";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { Tooltip } from "@/shared/ui/Tooltip";
import CommentItem from "./CommentItem.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import type { Comment } from "@/shared/api/models/common/comment";
import { useUiStore } from "@/shared/stores/ui";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));

/** The route the item reads its page number off; a test sets the query. */
const route = vi.hoisted(() => ({
  query: {} as Record<string, string>,
}));

vi.mock("vue-router", () => ({
  useRoute: () => route,
  RouterLink: { template: "<a><slot /></a>" },
}));

/**
 * The router's link as a bare anchor that keeps where it leads: the number of
 * a comment is such a link, and its destination is what it has to say.
 */
const RouterLinkStub = {
  name: "RouterLink",
  props: ["to"],
  template: "<a><slot /></a>",
};

// The editor is a heavy tiptap component; the edit-seeding contract only
// needs its modelValue.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    template: "<textarea class='editor-stub' :value='modelValue' />",
    props: ["modelValue"],
  },
}));

const comment = (over: Partial<Comment> = {}): Comment =>
  ({
    id: "c-1",
    // Server-rendered Display HTML — what the list shows, never edit source.
    text: "<strong>Правила</strong>",
    createdUtc: new Date().toISOString(),
    modifiedUtc: null,
    author: {
      username: "SolohinLex",
      role: "RegularUser",
      lastActivityUtc: null,
    },
    likes: [],
    isRemoved: false,
    ...over,
  }) as unknown as Comment;

/** A problem document with the one field these tests read. */
const refused = (status: number, title = "") => ({
  data: null,
  error: { type: "", title, status, traceId: "t" },
});

// A question left open by one test would answer the next one's clicks.
enableAutoUnmount(afterEach);

let submitEdit: ReturnType<typeof vi.fn>;
let submitDelete: ReturnType<typeof vi.fn>;
let submitLike: ReturnType<typeof vi.fn>;
let submitUnlike: ReturnType<typeof vi.fn>;

beforeEach(() => {
  route.query = {};
  localStorage.clear();
  // The signed-in author (auth store seeds itself from localStorage).
  localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
  submitEdit = vi.fn().mockResolvedValue({ error: null });
  submitDelete = vi.fn().mockResolvedValue({ error: null });
  submitLike = vi.fn().mockResolvedValue({ error: null });
  submitUnlike = vi.fn().mockResolvedValue({ error: null });
  // Error toasts never auto-dismiss and deduplicate by text.
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
});

const mountComponent = (
  fetchEditSource: (id: string) => Promise<unknown>,
  over: Partial<Comment> = {},
  extra: Record<string, unknown> = {},
) =>
  mount(CommentItem, {
    global: {
      plugins: [createPinia()],
      stubs: { RouterLink: RouterLinkStub },
    },
    props: {
      comment: comment(over),
      fetchEditSource: fetchEditSource as never,
      submitEdit: submitEdit as never,
      submitDelete: submitDelete as never,
      submitLike: submitLike as never,
      submitUnlike: submitUnlike as never,
      ...extra,
    },
  });

/** Every plate on screen, by its sentence. */
const plates = () => useToast().toasts.value.map((t) => t.message);

/** The refusal line of a place (shared/ui/ActionResult). */
const LINE = ".action-result";

/** An action of the footer's caption line, by its word. */
const action = (wrapper: VueWrapper, word: string) => {
  const found = wrapper
    .findAll(".caption-action")
    .find((one) => one.text() === word);
  if (!found) throw new Error(`"${word}" is not in the caption line`);
  return found;
};

/** The like the viewer may press: the shared badge as a toggle button. */
const HEART = "button.like-badge";

/** A part of the confirmation, which stands in <body> and not in the item. */
const inQuestion = (selector: string) => {
  const found = document.body.querySelector<HTMLElement>(
    `.dialog-container ${selector}`,
  );
  if (!found) throw new Error(`${selector} is not in the question`);
  return found;
};

describe("CommentItem edit seeding", () => {
  it("seeds the editor from the fetched raw BBCode source, not the displayed HTML", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(fetch).toHaveBeenCalledWith("c-1");
    const editor = wrapper.find(".editor-stub");
    expect(editor.exists()).toBe(true);
    expect((editor.element as HTMLTextAreaElement).value).toBe(
      "[b]Правила[/b]",
    );
  });

  it("does not open the editor when the source fetch fails", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: null,
      error: { message: "network" },
    });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  /**
   * The reader pressed "Редактировать" in this comment's footer and is
   * looking at it. The refusal used to arrive as a plate in the corner of the
   * page; now it stands under the footer, where the press was.
   */
  it("prints the refusal under the footer instead of raising a plate", async () => {
    const wrapper = mountComponent(vi.fn().mockResolvedValue(refused(404)));

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    const line = wrapper.find(LINE);
    expect(line.exists()).toBe(true);
    expect(line.text()).toBe("Не удалось загрузить текст комментария");
    expect(line.attributes("role")).toBe("alert");
    expect(plates()).toEqual([]);
  });

  it("prints what the server said when it said something", async () => {
    const wrapper = mountComponent(
      vi.fn().mockResolvedValue(refused(404, "Комментарий не найден")),
    );

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).text()).toBe("Комментарий не найден");
  });

  // The owner's rule of 26 September 2026: a failure the interceptor used to
  // put in a plate - a 5xx here - is said under the footer as well, and the
  // plate is taken down (useActionResult). Before, the line kept quiet on
  // these statuses and the reader got the refusal in the corner.
  it("says a failure the interceptor used to plate under the footer too", async () => {
    const wrapper = mountComponent(vi.fn().mockResolvedValue(refused(503)));

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).text()).toBe(
      "Не удалось загрузить текст комментария",
    );
    expect(plates()).toEqual([]);
  });

  // A dead connection is about the reader's network and not about the press:
  // its plate is the interceptor's, and the line stays empty.
  it("leaves a dead connection to its plate", async () => {
    const wrapper = mountComponent(vi.fn().mockResolvedValue(refused(0)));

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).exists()).toBe(false);
  });

  it("clears the refusal on the next press", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(refused(404))
      .mockResolvedValueOnce({
        data: { resource: comment({ text: "[b]Правила[/b]" }) },
        error: undefined,
      });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();
    expect(wrapper.find(LINE).exists()).toBe(true);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(LINE).exists()).toBe(false);
    expect(wrapper.find(".editor-stub").exists()).toBe(true);
  });

  it("hands the edited raw BBCode to the save function", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();
    await wrapper.find(".save-btn").trigger("click");
    await flushPromises();

    expect(submitEdit).toHaveBeenCalledWith("c-1", "[b]Правила[/b]");
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  /**
   * The editor used to close on the emit, whatever the server answered: the
   * rejected text was gone, the old text was back, and nothing was said.
   */
  it("keeps the editor open and names the refusal when the save fails", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    submitEdit.mockResolvedValue({
      error: { type: "", title: "", status: 404, traceId: "t" },
    });
    const wrapper = mountComponent(fetch);

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();
    await wrapper.find(".save-btn").trigger("click");
    await flushPromises();

    expect(wrapper.find(".editor-stub").exists()).toBe(true);
    // Under the buttons of the editor, where the press was; no plate.
    expect(wrapper.find(".edit-container " + LINE).text()).toBe(
      "Не удалось сохранить комментарий",
    );
    expect(plates()).toEqual([]);
  });

  /**
   * The question stays on screen with the refusal in it, over its buttons: it
   * used to close on any answer and leave a plate in the corner about a window
   * that was gone. The request carries the flags that keep the interceptor
   * from saying the same refusal a second time.
   */
  it("names the refusal in the question when the delete fails", async () => {
    submitDelete.mockResolvedValue({
      error: { type: "", title: "", status: 404, traceId: "t" },
    });
    const wrapper = mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    expect(submitDelete).toHaveBeenCalledWith("c-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    const dialog = wrapper.findComponent(ConfirmDialog);
    expect(dialog.props("show")).toBe(true);
    expect(inQuestion(".dialog-refusal").textContent?.trim()).toBe(
      "Не удалось удалить комментарий",
    );
    expect(plates()).toEqual([]);
    expect(wrapper.text()).not.toContain("Комментарий удален");
  });

  it("closes the question once the comment is gone", async () => {
    const wrapper = mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(false);
  });

  /**
   * The footer packs "Редактировать", "Удалить" and "Предупреждение" at a $small
   * step, and the middle one deleted on the first click with nothing to undo
   * it. The game post and the forum topic both ask first, through this dialog.
   */
  it("asks before deleting", async () => {
    const wrapper = mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");

    expect(submitDelete).not.toHaveBeenCalled();
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(true);
  });

  it("deletes nothing when the question is answered no", async () => {
    const wrapper = mountComponent(vi.fn());

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-cancel").click();
    await flushPromises();

    expect(submitDelete).not.toHaveBeenCalled();
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(false);
  });
});

/**
 * The heart reads the likes the list brought: the server used to send every
 * comment with none, so a reader's own like was gone after a reload, the heart
 * pressed "like" again and the refusal of that second like went unsaid.
 */
describe("CommentItem likes", () => {
  /** Signed in as someone other than the author, who may like. */
  const signInAs = (username: string) =>
    localStorage.setItem("user", JSON.stringify({ username }));

  const liker = (username: string) => ({ id: `u-${username}`, username });

  it("shows the count and the reader's own like as the list brought them", () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn(), {
      likes: [liker("Reader"), liker("Other")] as never,
    });

    const heart = wrapper.find(HEART);
    expect(heart.attributes("aria-pressed")).toBe("true");
    expect(heart.attributes("aria-label")).toBe("Нравится: 2");
    expect(heart.find(".like-badge__count").text()).toBe("2");
  });

  it("shows someone else's likes without claiming the reader's", () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn(), {
      likes: [liker("Other")] as never,
    });

    const heart = wrapper.find(HEART);
    expect(heart.attributes("aria-pressed")).toBe("false");
    expect(heart.find(".like-badge__count").text()).toBe("1");
  });

  // The case the empty list broke: after a reload the reader's like has to be
  // taken back, not left again.
  it("takes back a like that came with the list", async () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn(), {
      likes: [liker("Reader")] as never,
    });

    await wrapper.find(HEART).trigger("click");
    await flushPromises();

    expect(submitUnlike).toHaveBeenCalledWith("c-1");
    expect(submitLike).not.toHaveBeenCalled();
  });

  it("likes a comment the reader has not liked", async () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(HEART).trigger("click");
    await flushPromises();

    expect(submitLike).toHaveBeenCalledWith("c-1");
    expect(submitUnlike).not.toHaveBeenCalled();
  });

  /**
   * A refused like leaves the heart exactly as it was, so without a word the
   * press looks like it never happened. The server names the conflict.
   */
  it("says what the server answered when it refuses the like", async () => {
    signInAs("Reader");
    submitLike.mockResolvedValue(refused(409, "Вы уже поставили лайк"));
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(HEART).trigger("click");
    await flushPromises();

    // Under the footer the heart stands in; no plate.
    expect(wrapper.find(LINE).text()).toBe("Вы уже поставили лайк");
    expect(plates()).toEqual([]);
    expect(wrapper.find(HEART).attributes("aria-pressed")).toBe("false");
  });

  it("falls back to its own words for each half of the heart", async () => {
    signInAs("Reader");
    submitLike.mockResolvedValue(refused(404));
    const liking = mountComponent(vi.fn());
    await liking.find(HEART).trigger("click");
    await flushPromises();

    submitUnlike.mockResolvedValue(refused(404));
    const unliking = mountComponent(vi.fn(), {
      likes: [liker("Reader")] as never,
    });
    await unliking.find(HEART).trigger("click");
    await flushPromises();

    expect(liking.find(LINE).text()).toBe("Не удалось поставить лайк");
    expect(unliking.find(LINE).text()).toBe("Не удалось убрать лайк");
    expect(plates()).toEqual([]);
  });

  // A 403 (own comment, blacklist) used to be the interceptor's plate alone;
  // it is said under the footer now, in the server's words, and the plate is
  // taken down.
  it("says a refusal the interceptor used to plate under the footer", async () => {
    signInAs("Reader");
    submitLike.mockResolvedValue(refused(403, "Вы не можете поставить лайк"));
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(HEART).trigger("click");
    await flushPromises();

    expect(submitLike).toHaveBeenCalledTimes(1);
    expect(wrapper.find(LINE).text()).toBe("Вы не можете поставить лайк");
    expect(plates()).toEqual([]);
  });

  // A second press while the first is in flight would like the comment twice,
  // and the server refuses the second one.
  it("sends one request for two quick presses", async () => {
    signInAs("Reader");
    let answer: (value: unknown) => void = () => {};
    submitLike.mockReturnValue(new Promise((resolve) => (answer = resolve)));
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(HEART).trigger("click");
    await wrapper.find(HEART).trigger("click");
    answer({ error: null });
    await flushPromises();

    expect(submitLike).toHaveBeenCalledTimes(1);
    expect(plates()).toEqual([]);
  });
});

/**
 * The number of a comment is a link to it and nothing more (owner, 12 October
 * 2026): it leads to the comment's anchor in the view of the list the reader
 * is on, and stands in the right corner of the footer in both layouts. A press
 * goes there as a press on any link does, and its address is copied the way
 * the address of any link is, from the browser's own menu: no tooltip, no
 * clipboard, no plate.
 */
describe("CommentItem number", () => {
  /** The router link the number is, as opposed to any other on the item. */
  const numberLink = (wrapper: VueWrapper) => {
    const found = wrapper
      .findAllComponents(RouterLinkStub)
      .find((link) => link.classes().includes("comment-number"));
    if (!found) throw new Error("Номер комментария не ссылка");
    return found;
  };

  // The page number alone is not where the comment is: on a list the reader
  // has filtered or sorted, page 3 of the whole list holds other comments.
  // The link keeps the view the reader is on, so a press moves to the anchor
  // and the list stays as it was.
  it("leads to the comment's anchor in the view of the list the reader is on", () => {
    route.query = {
      number: "3",
      search: "кубы",
      authors: "Experienced",
      sortOrder: "desc",
    };
    const wrapper = mountComponent(vi.fn(), {}, { number: 12 });

    expect(numberLink(wrapper).props("to")).toEqual({
      query: {
        number: "3",
        search: "кубы",
        authors: "Experienced",
        sortOrder: "desc",
      },
      hash: "#comment-c-1",
    });
  });

  it("carries no page number on a page that has none", () => {
    const wrapper = mountComponent(vi.fn(), {}, { number: 12 });

    expect(numberLink(wrapper).props("to")).toEqual({
      query: {},
      hash: "#comment-c-1",
    });
  });

  // A guest reading a comment nobody liked has nothing else in the footer of
  // the full layout, and the number stands there all the same, without an
  // empty caption line beside it.
  it("stands last in the footer, outside its caption line, in both layouts", () => {
    localStorage.clear();
    for (const compact of [true, false]) {
      const wrapper = mountComponent(vi.fn(), {}, { number: 12, compact });

      const number = numberLink(wrapper).element;
      const footer = wrapper.get(".comment-footer").element;
      expect(number.parentElement).toBe(footer);
      expect(footer.lastElementChild).toBe(number);
      expect(number.closest(".caption-line")).toBeNull();
      expect(wrapper.find(".comment-header .comment-number").exists()).toBe(
        false,
      );
      expect(wrapper.find(".comment-caption").exists()).toBe(compact);
    }
  });

  // The router counts every link to the page it is on as the current page,
  // whatever the anchor: left to it, each number would tell a screen reader
  // "current page". The real router, not the stub, is what does this.
  it("is a link of the real router, not announced as the current page", async () => {
    const { createRouter, createMemoryHistory } =
      await vi.importActual<typeof import("vue-router")>("vue-router");
    const page = { render: () => null };
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: "/profile/:username", name: "profile", component: page },
        { path: "/:any(.*)*", component: page },
      ],
    });
    await router.push("/forum/newbies/3?number=2&authors=Experienced");
    route.query = { number: "2", authors: "Experienced" };

    const wrapper = mount(CommentItem, {
      global: { plugins: [createPinia(), router] },
      props: {
        comment: comment(),
        number: 12,
        fetchEditSource: vi.fn() as never,
        submitEdit: submitEdit as never,
        submitDelete: submitDelete as never,
        submitLike: submitLike as never,
        submitUnlike: submitUnlike as never,
      },
    });

    const link = wrapper.get("a.comment-number");
    expect(link.attributes("href")).toBe(
      "/forum/newbies/3?number=2&authors=Experienced#comment-c-1",
    );
    expect(link.attributes("aria-current")).toBeUndefined();
  });

  it("is pushed to the right edge of the footer", () => {
    const rules = styleRules(componentCss(resolve(HERE, "CommentItem.vue")));
    expect(
      rules.find((rule) => rule.selector === ".comment-number")?.declarations,
    ).toMatchObject({ "margin-left": "auto" });
  });

  it("copies nothing and raises no plate when pressed", async () => {
    const write = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText: write } });
    const wrapper = mountComponent(vi.fn(), {}, { number: 12 });

    await numberLink(wrapper).trigger("click");
    await flushPromises();

    expect(write).not.toHaveBeenCalled();
    expect(plates()).toEqual([]);
  });

  // A number alone does not say what it is to a screen reader, and the name is
  // not a tooltip: nothing appears under the pointer.
  it("has no tooltip, and a name that says which comment it is", () => {
    for (const compact of [true, false]) {
      const wrapper = mountComponent(vi.fn(), {}, { number: 12, compact });

      const link = numberLink(wrapper);
      expect(
        wrapper
          .findAllComponents(Tooltip)
          .some((tip) => tip.find(".comment-number").exists()),
      ).toBe(false);
      expect(link.attributes("title")).toBeUndefined();
      expect(link.attributes("aria-label")).toBe("Комментарий 12");
      expect(link.text()).toBe("12");
    }
  });

  // An answer of a ticket is not a comment, and its list says what it counts.
  it("is read out with the name the list gives what it counts", () => {
    const wrapper = mountComponent(vi.fn(), {}, { number: 3, noun: "Ответ" });

    expect(numberLink(wrapper).attributes("aria-label")).toBe("Ответ 3");
  });
});

/**
 * The footer of the compact layout is one caption line (shared/ui/CaptionLine):
 * the author and the dates, then every action the viewer may take, and the
 * like last of all, with " | " between the parts. The number is not a part of
 * it: it keeps its own corner. The full layout stands as a post does: the
 * author, the date and the like in the author's column, "Отредактировано" and
 * the actions on the edit line under the text, the number alone in the footer.
 */
describe("CommentItem caption line", () => {
  const signInAs = (username: string, role?: string) =>
    localStorage.setItem("user", JSON.stringify({ username, role }));

  const CREATED = "2026-09-09T06:33:00Z";
  const EDITED = "2026-09-09T08:06:00Z";

  /**
   * Runs of layout whitespace as one space. The no-break space a date keeps
   * after "в" is text, not layout, and stays what it is.
   */
  const collapse = (text: string) => text.replace(/[^\S\u00a0]+/g, " ");

  /** The parts of a caption line as a reader sees them, the like by name. */
  const parts = (line: Element | null) => {
    if (!line) throw new Error("no caption line");
    return [...line.querySelectorAll(":scope > .caption-line__part")].map(
      (part) =>
        part.querySelector(".like-badge")
          ? "[лайк]"
          : collapse(part.textContent ?? "")
              .replace(/ \|$/, "")
              .trim(),
    );
  };

  const footerLine = (wrapper: VueWrapper) =>
    wrapper.element.querySelector(".comment-footer .caption-line");

  const moderatorsComment = (extra: Record<string, unknown> = {}) =>
    mountComponent(
      vi.fn(),
      {
        createdUtc: CREATED as never,
        modifiedUtc: EDITED as never,
        author: {
          username: "TestMentor",
          role: "Moderator",
          lastActivityUtc: null,
        } as never,
        likes: [{ id: "u-1", username: "Other" }] as never,
      },
      { number: 7, ...extra },
    );

  // The author is a moderator, so the reader who is offered the warning on it
  // is a senior moderator: a warning reaches only below the reader's rank.
  it("runs the author, the dates, the actions and the like, in that order", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = moderatorsComment();

    expect(parts(footerLine(wrapper))).toEqual([
      // The comma ends the author's part: a phone breaks the line after it
      // and keeps the date whole on the next line.
      "Автор: TestMentor [M] [offline],",
      formatDateFull(CREATED),
      `Отредактировано ${formatDateFull(EDITED)}`,
      "Редактировать",
      "Удалить",
      "Предупреждение",
      "[лайк]",
    ]);
  });

  it("puts the like right after the date when the viewer has no action", () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn(), {
      createdUtc: CREATED as never,
      likes: [{ id: "u-1", username: "Other" }] as never,
    });

    expect(parts(footerLine(wrapper))).toEqual([
      "Автор: SolohinLex [offline],",
      formatDateFull(CREATED),
      "[лайк]",
    ]);
  });

  it("keeps the number out of the line, in its own corner", () => {
    signInAs("Mod", "Moderator");
    const wrapper = moderatorsComment();

    const number = wrapper.get(".comment-number").element;
    expect(number.closest(".caption-line")).toBeNull();
    expect(number.closest(".comment-footer")).not.toBeNull();
  });

  it("holds the edit and the actions under the text in the full layout, as a post does", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = moderatorsComment({ compact: false });

    const line = wrapper.get(".comment-body > .comment-edit-line");
    expect(parts(line.element.querySelector(".caption-line"))).toEqual([
      `Отредактировано ${formatDateFull(EDITED)}`,
      "Редактировать",
      "Удалить",
      "Предупреждение",
    ]);
    // The footer holds the number alone.
    expect(wrapper.find(".comment-footer .caption-line").exists()).toBe(false);
    expect(wrapper.get(".comment-footer").text()).toBe("7");
  });

  // The column of a post: the name, the picture, the date, and the like where
  // a post has the sum of its reviews.
  it("stands the name, the avatar, the date and the like in the author's column, in that order", () => {
    signInAs("Mod", "Moderator");
    const wrapper = moderatorsComment({ compact: false });

    const column = wrapper.get(".comment-header .author-inner");
    expect(
      [...column.element.children].map((child) => child.className),
    ).toEqual(["author-line", "avatar-link", "comment-date", "comment-like"]);
    expect(column.get(".comment-date").text()).toBe(formatDateFull(CREATED));
    expect(column.find(".comment-like .like-badge").exists()).toBe(true);
    expect(column.text()).not.toContain("Отредактировано");
  });

  it("draws no edit line where there is nothing to say", () => {
    localStorage.clear();
    const wrapper = mountComponent(vi.fn(), {}, { compact: false, number: 3 });

    expect(wrapper.find(".comment-edit-line").exists()).toBe(false);
  });

  // The author's own comment and a guest see the same drawing, not a control.
  it("draws the like for a guest without letting them press it", () => {
    localStorage.clear();
    const wrapper = mountComponent(vi.fn(), {
      likes: [{ id: "u-1", username: "Other" }] as never,
    });

    expect(wrapper.find(HEART).exists()).toBe(false);
    expect(wrapper.find(".like-badge").exists()).toBe(true);
    expect(parts(footerLine(wrapper)).at(-1)).toBe("[лайк]");
  });
});

/**
 * The text of a comment is set as the text of a post is: the old site kept both
 * in the same div.dmtxt. It is flush on both sides at every width, a phone
 * included, for a phone shows the page of the desktop, and "Перенос слов" of
 * the panel reaches it as it reaches a post.
 */
describe("CommentItem typesetting", () => {
  const textClasses = (wrapper: VueWrapper) =>
    wrapper.get(".comment-text").classes();

  it("hyphenates the text by default", () => {
    const wrapper = mountComponent(vi.fn());

    expect(textClasses(wrapper)).toContain("hyphenated");
  });

  it("drops hyphenation the reader turned off on a previous visit", () => {
    localStorage.setItem("dm_hyphenate_words", "off");
    const wrapper = mountComponent(vi.fn());

    expect(textClasses(wrapper)).not.toContain("hyphenated");
  });

  it("follows the panel without a reload", async () => {
    const wrapper = mountComponent(vi.fn());
    const ui = useUiStore();

    ui.setHyphenateWords(false);
    await flushPromises();

    expect(textClasses(wrapper)).not.toContain("hyphenated");
  });

  it("justifies at every width and hyphenates through the class alone", () => {
    const css = componentCss(resolve(HERE, "CommentItem.vue"));
    const merged = (selector: string) =>
      Object.assign(
        {},
        ...styleRules(css)
          .filter((rule) => rule.selector === selector)
          .map((rule) => rule.declarations),
      ) as Record<string, string>;

    // No width sets the comment apart: a phone shows the comment of 1366.
    expect(css).not.toMatch(/@media/);
    expect(merged(".comment-text")).toMatchObject({
      "line-height": "1.25",
      "text-align": "justify",
    });
    expect(merged(".comment-text.hyphenated")).toMatchObject({
      hyphens: "auto",
      "-webkit-hyphens": "auto",
    });
    // The switch turned off has to take the property with it, so the bare
    // block may not carry it on its own.
    expect(merged(".comment-text")).not.toHaveProperty("hyphens");
  });
});

/**
 * The full layout stands in the geometry of a post (GamePost.vue, DM2's
 * div.commentItem): the card's padding of 5, the author's column of 140 whose
 * content, 11px in from the top and 5px from the sides with 2px more on the
 * right, is 128 wide, a spacer of 10, the text 22px under the top of its
 * column and 10px in from its edges, the edit line 22px under the text, and
 * the number in a row of its own in the corner of the card. The compact layout
 * keeps its own.
 */
describe("CommentItem full layout", () => {
  const css = () => componentCss(resolve(HERE, "CommentItem.vue"));
  const of = (selector: string) =>
    Object.assign(
      {},
      ...styleRules(css())
        .filter((rule) => rule.selector.split(", ").includes(selector))
        .map((rule) => rule.declarations),
    ) as Record<string, string>;

  it("is the card and the columns of a post", () => {
    expect(of(".comment:not(.compact):not(.removed)")).toMatchObject({
      padding: "5px",
    });
    expect(of(".comment:not(.compact) .comment-columns")).toMatchObject({
      display: "flex",
      gap: "10px",
      "align-items": "flex-start",
    });
    expect(of(".comment-header")).toMatchObject({
      width: "140px",
      "flex-shrink": "0",
      "line-height": "1.25",
    });
    expect(of(".author-inner")).toMatchObject({
      margin: "11px 5px",
      "padding-right": "2px",
    });
    // The avatar fills the column, as the portrait of a post does.
    expect(of(".avatar")).toMatchObject({
      width: "100%",
      "box-sizing": "border-box",
    });
    expect(of(".avatar")).not.toHaveProperty("border-radius");
  });

  it("sets the text where a post sets it, and the edit line under it", () => {
    expect(of(".comment:not(.compact) .comment-body")).toMatchObject({
      padding: "11px 0",
    });
    expect(of(".comment:not(.compact) .comment-text")).toMatchObject({
      margin: "11px 10px",
    });
    expect(of(".comment-edit-line")).toMatchObject({
      margin: "11px 5px 0 10px",
      "font-size": "12px",
      "line-height": "15px",
    });
    expect(of(".comment:not(.compact) .comment-footer")).toMatchObject({
      "margin-top": "0",
      "line-height": "15px",
    });
  });

  it("puts the author's column beside the text and the number under both", () => {
    localStorage.clear();
    const wrapper = mountComponent(vi.fn(), {}, { compact: false, number: 3 });

    const columns = wrapper.get(".comment-columns").element;
    expect([...columns.children].map((child) => child.className)).toEqual([
      "comment-header",
      "comment-body",
    ]);
    const footer = wrapper.get(".comment-footer").element;
    expect(footer.parentElement).toBe(wrapper.element);
    expect(columns.compareDocumentPosition(footer)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it("asks for the avatar at the width it is drawn", () => {
    const wrapper = mountComponent(
      vi.fn(),
      {
        author: {
          username: "SolohinLex",
          role: "RegularUser",
          lastActivityUtc: null,
          picture: { smallUrl: "/s.png", mediumUrl: "/m.png" },
        } as never,
      },
      { compact: false },
    );

    expect(wrapper.get("img.avatar").attributes("sizes")).toBe("128px");
  });

  it("keeps the compact layout without a column", () => {
    const wrapper = mountComponent(vi.fn(), {}, { compact: true, number: 3 });

    expect(wrapper.find(".comment-header").exists()).toBe(false);
    expect(wrapper.find(".avatar-link").exists()).toBe(false);
    expect(wrapper.find(".comment-edit-line").exists()).toBe(false);
    expect(wrapper.find(".comment-footer .comment-caption").exists()).toBe(
      true,
    );
  });
});

/**
 * "Аккаунт удален" reads as every other name of its place (the owner, 12.10:
 * "Делай его везде как все остальное"). The full layout names its author in
 * bold, in the colour of the text (.author-name), and the removed account
 * stood in the same place as a plain link, of a weight and a colour of its
 * own.
 */
describe("CommentItem author", () => {
  const nameIn = (wrapper: VueWrapper) => wrapper.get(".comment-header a");

  it("names a removed account in the full layout the way it names an author", () => {
    const living = nameIn(mountComponent(vi.fn(), {}, { compact: false }));
    const removed = nameIn(
      mountComponent(vi.fn(), { author: null as never }, { compact: false }),
    );

    expect(living.text()).toBe("SolohinLex");
    expect(removed.text()).toBe("Аккаунт удален");
    expect(removed.classes()).toEqual(living.classes());
    expect(removed.classes()).toContain("author-name");
  });
});

/**
 * The warning window is the list's: the item only says which comment the
 * moderator pressed "Предупреждение" under, and the number it shows (the list
 * opens WarningDialog with its author, its text and that number linked to it,
 * useCommentWarnDialog).
 */
describe("CommentItem warning", () => {
  const signInAs = (username: string, role?: string) =>
    localStorage.setItem("user", JSON.stringify({ username, role }));

  const mountItem = (over: Partial<Comment> = {}, number?: number) =>
    mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: { template: "<a><slot /></a>" } },
      },
      props: {
        comment: comment(over),
        fetchEditSource: vi.fn() as never,
        submitEdit: submitEdit as never,
        submitDelete: submitDelete as never,
        submitLike: submitLike as never,
        submitUnlike: submitUnlike as never,
        number,
      },
    });

  it("asks the list for the warning window about this comment", async () => {
    signInAs("Mod", "Moderator");
    const wrapper = mountItem({}, 12);

    await action(wrapper, "Предупреждение").trigger("click");

    // The window links the number to the comment, and the list hands the
    // window what the item emits, so the number leaves with the press.
    expect(wrapper.emitted("warn")).toEqual([["c-1", 12]]);
  });

  /**
   * A warning reaches only a rank below the reader's own and never an
   * administrator (the server's rank rule): a moderator is not offered one
   * under the text of a colleague, a senior moderator or an administrator,
   * which the server would refuse (the owner, 05.10: "нет").
   */
  it.each(["Moderator", "SeniorModerator", "Admin"])(
    "offers a moderator no warning about a comment of a %s",
    (role) => {
      signInAs("Mod", "Moderator");
      const wrapper = mountItem({
        author: { username: "Staff", role, lastActivityUtc: null } as never,
      });

      expect(
        wrapper.findAll(".caption-action").map((one) => one.text()),
      ).not.toContain("Предупреждение");
    },
  );

  it("offers a senior moderator the warning about a comment of a moderator", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = mountItem({
      author: {
        username: "Staff",
        role: "Moderator",
        lastActivityUtc: null,
      } as never,
    });

    expect(
      wrapper.findAll(".caption-action").map((one) => one.text()),
    ).toContain("Предупреждение");
  });

  it("offers no warning about a comment whose account was removed", () => {
    signInAs("Mod", "Moderator");
    const wrapper = mountItem({ author: null as never });

    expect(
      wrapper.findAll(".caption-action").map((one) => one.text()),
    ).not.toContain("Предупреждение");
  });
});

/**
 * The reader on the blacklist of the game or blog the discussion belongs to.
 * Of what the item offers, the list closes only the rewrite of one's own
 * comment: the heart is the heart everybody gets, and "Удалить" stays.
 */
describe("CommentItem on a blacklist", () => {
  const signInAs = (username: string) =>
    localStorage.setItem("user", JSON.stringify({ username }));

  const onTheList = (over: Partial<Comment> = {}) =>
    mountComponent(vi.fn(), over, { viewerBlacklisted: true });

  const actions = (wrapper: VueWrapper) =>
    wrapper.findAll(".caption-action").map((one) => one.text());

  it("offers the heart on somebody else's comment as it does to anybody", async () => {
    signInAs("Reader");
    const wrapper = onTheList({
      likes: [{ id: "u-1", username: "Other" }] as never,
    });

    await wrapper.get(HEART).trigger("click");
    await flushPromises();

    expect(submitLike).toHaveBeenCalledWith("c-1");
  });

  it("keeps Удалить and takes Редактировать off the reader's own comment", () => {
    // Signed in as the author (the default of this file), inside the quarter
    // of an hour in which both are offered.
    const free = mountComponent(vi.fn());
    expect(actions(free)).toEqual(["Редактировать", "Удалить"]);

    const listed = onTheList();
    expect(actions(listed)).toEqual(["Удалить"]);
  });
});

/**
 * Every part of a comment stands at every width (owner, 12 October 2026: no
 * "Еще", no "…", no name cut, on a phone the full version). A row that has no
 * room wraps between its parts, as it did on a desktop before 5 October, and
 * each part goes down whole. jsdom lays nothing out, so what is read here is
 * what the rows are made of; the wrap itself, at the widths of a phone, is
 * read in a browser (e2e common/mobile-full-posts.spec.ts).
 */
describe("CommentItem at every width", () => {
  const signInAs = (username: string, role?: string) =>
    localStorage.setItem("user", JSON.stringify({ username, role }));

  const CREATED = "2026-09-09T06:33:00Z";
  const EDITED = "2026-09-09T08:06:00Z";
  /** Runs of layout whitespace as one space, the no-break one included. */
  const spaced = (text: string) => text.replace(/\s+/g, " ").trim();

  /** A moderator's edited comment as a senior moderator reads it. */
  const moderatorsComment = (compact: boolean) => {
    signInAs("Senior", "SeniorModerator");
    return mountComponent(
      vi.fn(),
      {
        createdUtc: CREATED as never,
        modifiedUtc: EDITED as never,
        author: {
          username: "TestMentor",
          role: "Moderator",
          lastActivityUtc: null,
        } as never,
        likes: [{ id: "u-1", username: "Other" }] as never,
      },
      { compact, number: 7 },
    );
  };

  const rules = () =>
    styleRules(componentCss(resolve(HERE, "CommentItem.vue")));

  /** The declarations of the rule that names the selector, alone or grouped. */
  const ruleOf = (selector: string) =>
    Object.assign(
      {},
      ...rules()
        .filter((rule) => rule.selector.split(", ").includes(selector))
        .map((rule) => rule.declarations),
    ) as Record<string, string>;

  // The caption stands in its block as text, so the block takes the
  // caption's size: on the text's 16 the line would be the text's height.
  it("sets the caption block in the caption's size", () => {
    expect(
      rules().find((rule) => rule.selector === ".comment-caption")
        ?.declarations,
    ).toMatchObject({ "font-size": "12px", "min-width": "0" });
  });

  it("puts nothing away and scrolls no row, in either layout", () => {
    for (const compact of [true, false]) {
      const wrapper = moderatorsComment(compact);
      const body = wrapper.get(".comment-body");

      expect(body.find(".scroll-row").exists()).toBe(false);
      expect(body.find(".more-menu").exists()).toBe(false);
      expect(body.find("[hidden]").exists()).toBe(false);
      expect(spaced(body.text())).not.toMatch(/Еще|…/);
      // Nothing of the queue of a row that put parts away is left on them.
      const queued = [body.element, ...body.element.querySelectorAll("*")]
        .flatMap((element) => [...element.attributes])
        .filter((attribute) => attribute.name.startsWith("data-fit"))
        .map((attribute) => attribute.name);
      expect(queued).toEqual([]);
      for (const name of body.findAll(".author-name, .author-link")) {
        expect(name.attributes("title")).toBeUndefined();
        expect(name.attributes("style")).toBeUndefined();
      }
      wrapper.unmount();
    }
  });

  // The name, the letter of the role and the mark of presence, each a part of
  // the line, with the line's own space between two of them: the one place it
  // breaks, and what the line copies as.
  it("keeps the name, the letter of the role and [offline] in the header, a space of the line apart", () => {
    const wrapper = moderatorsComment(false);
    const line = wrapper.get(".comment-header .author-line");

    expect(
      [...line.element.children].map((child) => [
        child.className,
        child.className === "author-gap"
          ? child.textContent
          : spaced(child.textContent ?? ""),
      ]),
    ).toEqual([
      ["author-name", "TestMentor"],
      ["author-gap", " "],
      ["author-role", "[M]"],
      ["author-gap", " "],
      ["author-presence", "[offline]"],
    ]);
    expect(line.text()).toBe("TestMentor [M] [offline]");
    expect(line.find(".copy-space").exists()).toBe(false);
  });

  it("names a removed account in the header without a mark of presence", () => {
    const wrapper = mountComponent(
      vi.fn(),
      { author: null as never },
      { compact: false },
    );

    const line = wrapper.get(".comment-header .author-line");
    expect([...line.element.children].map((child) => child.className)).toEqual([
      "author-name",
    ]);
    expect(line.text()).toBe("Аккаунт удален");
  });

  // Each part one box as wide as its text, which goes down whole and wraps
  // inside only where it is wider than the whole line; nowrap kept such a
  // part past the edge, and no rule here cuts a name.
  it("sets each part of the header a box no wider than the line, and cuts nothing", () => {
    for (const part of [".author-name", ".author-role", ".author-presence"]) {
      expect(ruleOf(part)).toMatchObject({
        display: "inline-block",
        "max-width": "100%",
      });
    }
    expect(ruleOf(".author-gap")["word-spacing"]).toMatch(/^calc\(2px - /);

    const css = componentCss(resolve(HERE, "CommentItem.vue"));
    expect(css).not.toMatch(/text-overflow|line-clamp/);
    // One line is held whole: the date, of a fixed width that ends inside the
    // column. No name is.
    expect(
      rules()
        .filter((rule) => /nowrap/.test(rule.declarations["white-space"] ?? ""))
        .map((rule) => rule.selector),
    ).toEqual([".comment-date"]);
  });

  it("writes the date of the author's column with its time in one text", () => {
    const wrapper = moderatorsComment(false);
    const date = wrapper.get(".comment-header .comment-date");

    expect(spaced(date.text())).toBe(spaced(formatDateFull(CREATED)));
    expect(date.element.children.length).toBe(0);
  });

  it("writes the date of the compact caption with its time in one part", () => {
    const wrapper = moderatorsComment(true);
    const date = wrapper
      .findAll(".comment-footer .caption-line__part")
      .find(
        (part) =>
          /\d{2}\.\d{2}\.\d{4}/.test(part.text()) && !/Автор/.test(part.text()),
      )!;

    expect(spaced(date.text())).toBe(spaced(formatDateFull(CREATED)));
    expect(date.element.firstElementChild?.children.length).toBe(0);
  });
});

/**
 * The comment of a ticket's conversation (TicketConversation): the comment of
 * a topic, read only. A ticket is the record of how it was settled (D6), so
 * nothing in it is edited, deleted, liked or warned about, by anybody, and
 * the item is handed none of the functions that would do it.
 */
describe("CommentItem read only", () => {
  const signInAs = (username: string, role?: string) =>
    localStorage.setItem("user", JSON.stringify({ username, role }));

  const readOnly = (
    over: Partial<Comment> = {},
    extra: Record<string, unknown> = {},
  ) =>
    mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: RouterLinkStub },
      },
      props: { comment: comment(over), readonly: true, number: 3, ...extra },
    });

  // A senior moderator is offered all of it on a user's comment of a topic.
  it("offers nothing to press, in either layout, even to a senior moderator", () => {
    signInAs("Senior", "SeniorModerator");
    for (const compact of [true, false]) {
      const wrapper = readOnly(
        { likes: [{ id: "u-1", username: "Other" }] as never },
        { compact },
      );

      expect(wrapper.find(".caption-action").exists()).toBe(false);
      expect(wrapper.find(".like-badge").exists()).toBe(false);
      expect(wrapper.text()).not.toMatch(
        /Редактировать|Удалить|Предупреждение/,
      );
      wrapper.unmount();
    }
  });

  it("keeps its number in the corner, and no empty line beside it", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = readOnly({}, { compact: false });

    expect(wrapper.get(".comment-footer .comment-number").text()).toBe("3");
    expect(wrapper.find(".comment-caption").exists()).toBe(false);
    expect(wrapper.find(".comment-edit-line").exists()).toBe(false);
  });

  it("keeps the author and the date in the compact caption", () => {
    const wrapper = readOnly(
      { createdUtc: "2026-09-09T06:33:00Z" as never },
      { compact: true },
    );

    expect(
      wrapper
        .findAll(".comment-footer .caption-line__part")
        .map((part) => part.text().replace(/\s+/g, " ").trim()),
    ).toEqual([
      "Автор: SolohinLex [offline],",
      formatDateFull("2026-09-09T06:33:00Z").replace(/\s+/g, " "),
    ]);
  });
});

/**
 * The guest who filed a ticket writes in its conversation without an account
 * (D6): his comment comes with no author, as a removed account's does, and
 * says "Гость" where the name stands, with no link, for there is no profile
 * to lead to, and no mark of presence.
 */
describe("CommentItem of a guest", () => {
  const guests = (compact: boolean) =>
    mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: RouterLinkStub },
      },
      props: {
        comment: comment({ author: null as never }),
        readonly: true,
        guest: true,
        compact,
      },
    });

  it("signs the header with the word, in the name's place and weight", () => {
    const wrapper = guests(false);
    const line = wrapper.get(".comment-header .author-line");

    expect(line.text()).toBe("Гость");
    expect(
      [...line.element.children].map((child) => [
        child.tagName,
        child.className,
      ]),
    ).toEqual([["SPAN", "author-name"]]);
  });

  it("leads nowhere from the picture or the word", () => {
    const wrapper = guests(false);

    expect(wrapper.findAllComponents(RouterLinkStub)).toEqual([]);
    expect(wrapper.get(".avatar-link").element.tagName).toBe("SPAN");
    expect(
      wrapper.get(".avatar-link .default-avatar").attributes("aria-label"),
    ).toBe("Гость");
  });

  it("signs the compact caption with the word", () => {
    const wrapper = guests(true);

    expect(
      wrapper.get(".comment-footer .author-info").text().replace(/\s+/g, " "),
    ).toBe("Автор: Гость");
    expect(wrapper.findAllComponents(RouterLinkStub)).toEqual([]);
  });

  it("is a removed account where the comment does not say it is a guest's", () => {
    const wrapper = mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: RouterLinkStub },
      },
      props: {
        comment: comment({ author: null as never }),
        readonly: true,
        compact: false,
      },
    });

    expect(wrapper.get(".comment-header .author-line").text()).toBe(
      "Аккаунт удален",
    );
  });
});
