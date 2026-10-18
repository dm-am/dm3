/**
 * @vitest-environment jsdom
 */

/**
 * "Редактировать" on a topic, and what the reader is told when the text it
 * needs does not arrive.
 *
 * The card shows server-rendered HTML; the editor needs the BBCode source,
 * which is a request of its own between the press and the editor. When that
 * request is refused the press did nothing visible, and the refusal used to
 * be a plate in the corner of the page - the one place a reader who has just
 * pressed a button in the card's footer is not looking. It is now a line under
 * the card, the shape the comment item already prints: quiet for the statuses
 * the interceptor announces itself (one refusal, one carrier) and cleared by
 * the next press.
 *
 * No plate is raised in any test here: the interceptor lives in the axios
 * instance and the api is doubled above it, so what is measured is that the
 * card itself says nothing in the corner.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia } from "pinia";
import { forumApi } from "@/entities/forum";
import type { Topic } from "@/entities/forum";
import { useToast } from "@/shared/lib/composables/useToast";
import TopicView from "./TopicView.vue";

// The editor is a heavy tiptap component; the seeding contract only needs its
// modelValue.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    template: "<textarea class='editor-stub' :value='modelValue' />",
    props: ["modelValue"],
  },
}));

// The digest boards read the leaderboards, and no topic here is a digest.
vi.mock("@/features/leaderboard/@x/topic", () => ({
  PeriodDigestBoards: { template: "<div />" },
}));

const topic = (): Topic =>
  ({
    id: "t-1",
    topicNumber: 1,
    author: {
      username: "SolohinLex",
      role: "RegularUser",
      lastActivityUtc: null,
    },
    createdUtc: new Date().toISOString(),
    modifiedUtc: null,
    title: "Правила",
    // Server-rendered Display HTML: what the card shows, never edit source.
    description: "<strong>Правила</strong>",
    isAttached: false,
    attachOrder: null,
    isClosed: false,
    closedByAuthor: false,
    lastActivityUtc: new Date().toISOString(),
    lastComment: null,
    commentsCount: 0,
    unreadCommentsCount: 0,
    board: { id: "b-1", alias: "general", title: "Общий", moderators: [] },
    likes: [],
    likesCount: 0,
  }) as unknown as Topic;

/** A problem document with the fields the card reads. */
const refused = (status: number, title = "") =>
  ({ data: null, error: { type: "", title, status, traceId: "t" } }) as never;

/** The topic as the AuthorEdit audience answers it: BBCode, not HTML. */
const source = () =>
  ({
    data: { resource: { ...topic(), description: "[b]Правила[/b]" } },
    error: null,
  }) as never;

const fetchSource = () => vi.spyOn(forumApi, "getTopicForUpdate");

/** The page answer the edit form waits for; a success unless told otherwise. */
const accepted = () => vi.fn().mockResolvedValue({ error: null });

function render(
  props: {
    topic?: Topic;
    submitEdit?: ReturnType<typeof accepted>;
  } = {},
) {
  return mount(TopicView, {
    props: { topic: topic(), submitEdit: accepted(), ...props },
    global: {
      plugins: [createPinia()],
      stubs: { RouterLink: { template: "<a><slot /></a>" } },
    },
  });
}

const editButton = (wrapper: VueWrapper) =>
  wrapper.findAll("button").find((b) => b.text() === "Редактировать")!;

async function pressEdit(wrapper: VueWrapper) {
  await editButton(wrapper).trigger("click");
  await flushPromises();
}

/** Every plate on screen, by its sentence. */
const plates = () => useToast().toasts.value.map((t) => t.message);

describe("TopicView edit source", () => {
  beforeEach(() => {
    localStorage.clear();
    // The signed-in author (auth store seeds itself from localStorage), so
    // the footer offers the edit.
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    // Error toasts never auto-dismiss and deduplicate by text.
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  afterEach(() => vi.restoreAllMocks());

  it("prints the refusal under the card instead of raising a plate", async () => {
    fetchSource().mockResolvedValue(refused(404, "Топик не найден"));
    const wrapper = render();

    await pressEdit(wrapper);

    const line = wrapper.get(".action-result");
    expect(line.text()).toBe("Топик не найден");
    expect(line.attributes("role")).toBe("alert");
    // The press did nothing else: no editor over a text that did not arrive.
    expect(wrapper.find(".topic-edit").exists()).toBe(false);
    expect(plates()).toEqual([]);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    fetchSource().mockResolvedValue(refused(404));
    const wrapper = render();

    await pressEdit(wrapper);

    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось загрузить текст топика",
    );
  });

  // One refusal, one carrier, and the carrier is the line under the card: the
  // refusals the interceptor used to plate are said here, and the plate is
  // taken down (the owner's rule of 26 September 2026).
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [503, "Ошибка сервера"],
  ])("says a refusal with status %i under the card", async (status, line) => {
    fetchSource().mockResolvedValue(refused(status, "Ошибка сервера"));
    const wrapper = render();

    await pressEdit(wrapper);

    expect(wrapper.get(".action-result").text()).toBe(line);
    expect(wrapper.find(".topic-edit").exists()).toBe(false);
    expect(plates()).toEqual([]);
  });

  // An expired session and a dead connection are about the reader, not the
  // press: their plate is the interceptor's, and the card keeps quiet.
  it.each([0, 401])(
    "stays quiet about a refusal with status %i, which the interceptor announced",
    async (status) => {
      fetchSource().mockResolvedValue(refused(status, "Ошибка сервера"));
      const wrapper = render();

      await pressEdit(wrapper);

      expect(wrapper.find(".action-result").exists()).toBe(false);
      expect(wrapper.find(".topic-edit").exists()).toBe(false);
      expect(plates()).toEqual([]);
    },
  );

  it("clears the refusal on the next press, which opens the editor on the source", async () => {
    fetchSource()
      .mockResolvedValueOnce(refused(404))
      .mockResolvedValueOnce(source());
    const wrapper = render();

    await pressEdit(wrapper);
    expect(wrapper.find(".action-result").exists()).toBe(true);

    await pressEdit(wrapper);

    expect(wrapper.find(".action-result").exists()).toBe(false);
    const editor = wrapper.find(".editor-stub");
    expect(editor.exists()).toBe(true);
    expect((editor.element as HTMLTextAreaElement).value).toBe(
      "[b]Правила[/b]",
    );
  });
});

/**
 * Closing a topic is a setting of the edit form, not a word of the caption
 * line. The form offers it to those the server lets close a topic, the board's
 * moderators, administrators and the topic's own author (TopicIntention.Close),
 * and saves it in the same request as the title and text. Anybody else, a
 * global moderator of another board among them, gets the form without it: the
 * server refuses them a change of the closed state, and in one request that
 * refusal would take the title and text down with it. The author gets the
 * form on an open topic and on one they closed themselves, which they reopen
 * from it. A topic closed by a board moderator or an administrator is shut to
 * the author: the server refuses them both its edits and its reopening.
 */
describe("TopicView: closing lives in the edit form", () => {
  const moderated = (overrides: Partial<Topic> = {}): Topic =>
    ({
      ...topic(),
      board: {
        id: "b-1",
        alias: "general",
        title: "Общий",
        moderators: [{ username: "BoardMod" }],
      },
      ...overrides,
    }) as unknown as Topic;

  const signIn = (user: { username: string; role?: string }) =>
    localStorage.setItem("user", JSON.stringify(user));

  const caption = (wrapper: VueWrapper) =>
    (wrapper.get(".caption-line").element.textContent ?? "")
      .replace(/\s+/g, " ")
      .trim();

  const closedBox = (wrapper: VueWrapper) =>
    wrapper
      .findAll("label.checkbox-label")
      .find((l) => l.text() === "Топик закрыт")
      ?.find("input[type='checkbox']");

  const save = async (wrapper: VueWrapper) => {
    await wrapper
      .findAll("button")
      .find((b) => b.text() === "Сохранить")!
      .trigger("click");
    await flushPromises();
  };

  beforeEach(() => {
    localStorage.clear();
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
    fetchSource().mockResolvedValue(source());
  });

  afterEach(() => vi.restoreAllMocks());

  it("offers no Закрыть or Открыть in the caption line", () => {
    signIn({ username: "BoardMod", role: "Moderator" });

    const open = render({ topic: moderated() });
    const closed = render({ topic: moderated({ isClosed: true }) });

    expect(caption(open)).toContain("Редактировать");
    expect(caption(open)).not.toContain("Закрыть");
    expect(caption(closed)).toContain("Редактировать");
    expect(caption(closed)).not.toContain("Открыть");
  });

  it("gives a board moderator the Топик закрыт box, set from the topic", async () => {
    signIn({ username: "BoardMod", role: "RegularUser" });
    const wrapper = render({ topic: moderated({ isClosed: true }) });

    await pressEdit(wrapper);

    const box = closedBox(wrapper);
    expect(box?.exists()).toBe(true);
    expect((box!.element as HTMLInputElement).checked).toBe(true);
  });

  it("gives an administrator the box on a board he does not moderate", async () => {
    signIn({ username: "Admin1", role: "Admin" });
    const wrapper = render();

    await pressEdit(wrapper);

    const box = closedBox(wrapper);
    expect(box?.exists()).toBe(true);
    expect((box!.element as HTMLInputElement).checked).toBe(false);
  });

  it("shows a global moderator of another board the form without the box", async () => {
    signIn({ username: "GlobalMod", role: "Moderator" });
    const wrapper = render({ topic: moderated() });

    await pressEdit(wrapper);

    expect(wrapper.find(".topic-edit").exists()).toBe(true);
    expect(closedBox(wrapper)).toBeUndefined();
  });

  it("offers a stranger neither the form nor the box", () => {
    signIn({ username: "Stranger" });
    const wrapper = render({ topic: moderated() });

    expect(editButton(wrapper)).toBeUndefined();
    expect(closedBox(wrapper)).toBeUndefined();
  });

  // A deleted account leaves the topic without an author, and a guest has no
  // name either: two absent names are not the same person.
  it("does not take a guest for the author of a topic whose account is gone", () => {
    const wrapper = render({
      topic: moderated({ author: null } as unknown as Partial<Topic>),
    });

    expect(editButton(wrapper)).toBeUndefined();
    expect(caption(wrapper)).not.toContain("Удалить");
  });

  it("gives the author the box and closes his topic in the same save", async () => {
    signIn({ username: "SolohinLex" });
    const submitEdit = accepted();
    const wrapper = render({ topic: moderated(), submitEdit });

    await pressEdit(wrapper);
    const box = closedBox(wrapper);
    expect((box!.element as HTMLInputElement).checked).toBe(false);
    await box!.setValue(true);
    await save(wrapper);

    expect(submitEdit).toHaveBeenCalledWith("t-1", {
      title: "Правила",
      description: "[b]Правила[/b]",
      isClosed: true,
    });
  });

  // The author reopens what they closed: the reopening arrives with the text of
  // the form (TopicIntention.Close), so the form is offered as on an open topic.
  it("gives the author the form and the box on a topic they closed themselves", async () => {
    signIn({ username: "SolohinLex" });
    const submitEdit = accepted();
    const wrapper = render({
      topic: moderated({
        isClosed: true,
        closedByAuthor: true,
      } as unknown as Partial<Topic>),
      submitEdit,
    });

    await pressEdit(wrapper);
    const box = closedBox(wrapper);
    expect((box!.element as HTMLInputElement).checked).toBe(true);
    await box!.setValue(false);
    await save(wrapper);

    expect(submitEdit).toHaveBeenCalledWith("t-1", {
      title: "Правила",
      description: "[b]Правила[/b]",
      isClosed: false,
    });
  });

  // What a board moderator or an administrator closed the author neither edits
  // (TopicIntention.Edit) nor reopens (TopicIntention.Close): a save from the
  // form would come back 403. They keep "Удалить" there.
  it("offers the author neither the form nor the box on a topic a moderator closed", () => {
    signIn({ username: "SolohinLex" });
    const wrapper = render({ topic: moderated({ isClosed: true }) });

    expect(editButton(wrapper)).toBeUndefined();
    expect(closedBox(wrapper)).toBeUndefined();
    expect(caption(wrapper)).toContain("Удалить");
  });

  // An author who is a global moderator edits the topic a board moderator
  // closed, but its reopening is refused to them, and in one request that
  // refusal would take the title and text down with it.
  it("shows an author who is a global moderator the form without the box on a topic a moderator closed", async () => {
    signIn({ username: "SolohinLex", role: "Moderator" });
    const wrapper = render({ topic: moderated({ isClosed: true }) });

    await pressEdit(wrapper);

    expect(wrapper.find(".topic-edit").exists()).toBe(true);
    expect(closedBox(wrapper)).toBeUndefined();
  });

  it("closes the topic in the same save as the title and text", async () => {
    signIn({ username: "BoardMod" });
    const submitEdit = accepted();
    const wrapper = render({ topic: moderated(), submitEdit });

    await pressEdit(wrapper);
    await closedBox(wrapper)!.setValue(true);
    await save(wrapper);

    expect(submitEdit).toHaveBeenCalledTimes(1);
    // The pin a board moderator has in the form rides along as it stood.
    expect(submitEdit).toHaveBeenCalledWith("t-1", {
      title: "Правила",
      description: "[b]Правила[/b]",
      isClosed: true,
      isAttached: false,
    });
    expect(wrapper.find(".topic-edit").exists()).toBe(false);
  });

  it("reopens a closed topic when the box is cleared", async () => {
    signIn({ username: "BoardMod" });
    const submitEdit = accepted();
    const wrapper = render({
      topic: moderated({ isClosed: true }),
      submitEdit,
    });

    await pressEdit(wrapper);
    await closedBox(wrapper)!.setValue(false);
    await save(wrapper);

    expect(submitEdit).toHaveBeenCalledWith(
      "t-1",
      expect.objectContaining({ isClosed: false }),
    );
  });

  it("sends no closed state from a global moderator of another board, which the server would refuse", async () => {
    signIn({ username: "GlobalMod", role: "Moderator" });
    const submitEdit = accepted();
    const wrapper = render({ topic: moderated(), submitEdit });

    await pressEdit(wrapper);
    await save(wrapper);

    expect(submitEdit).toHaveBeenCalledTimes(1);
    expect(submitEdit.mock.calls[0][1]).not.toHaveProperty("isClosed");
  });

  it("keeps the form and the ticked box when the save is refused", async () => {
    signIn({ username: "BoardMod" });
    const submitEdit = vi.fn().mockResolvedValue({
      error: { type: "", title: "Топик не найден", status: 404, traceId: "t" },
    });
    const wrapper = render({ topic: moderated(), submitEdit });

    await pressEdit(wrapper);
    await closedBox(wrapper)!.setValue(true);
    await save(wrapper);

    expect(wrapper.find(".topic-edit").exists()).toBe(true);
    expect((closedBox(wrapper)!.element as HTMLInputElement).checked).toBe(
      true,
    );
    // Under the buttons of the form; no plate in the corner.
    expect(wrapper.get(".topic-edit .action-result").text()).toBe(
      "Топик не найден",
    );
    expect(plates()).toEqual([]);
  });
});

/**
 * Pinning in the edit form, under "Топик закрыт". The list of the board pins
 * with the pin before the title, and the topic page had no way to it at all;
 * the form has a box for it now, as it has for closing. The server lets the
 * board's moderators and administrators pin (ForumIntention.AdministrateTopics)
 * and nobody else: not the author, who closes his own topic, and not a
 * moderator of the site who does not moderate this board. A refused change of
 * the pin would take the title and the text of the same request down with it,
 * so the box is not offered to them and the pin is not sent by them.
 */
describe("TopicView: pinning lives in the edit form", () => {
  const moderated = (overrides: Partial<Topic> = {}): Topic =>
    ({
      ...topic(),
      board: {
        id: "b-1",
        alias: "general",
        title: "Общий",
        moderators: [{ username: "BoardMod" }],
      },
      ...overrides,
    }) as unknown as Topic;

  const signIn = (user: { username: string; role?: string }) =>
    localStorage.setItem("user", JSON.stringify(user));

  const boxes = (wrapper: VueWrapper) =>
    wrapper.findAll("label.checkbox-label").map((label) => label.text());

  const pinnedBox = (wrapper: VueWrapper) =>
    wrapper
      .findAll("label.checkbox-label")
      .find((l) => l.text() === "Топик закреплен")
      ?.find("input[type='checkbox']");

  const save = async (wrapper: VueWrapper) => {
    await wrapper
      .findAll("button")
      .find((b) => b.text() === "Сохранить")!
      .trigger("click");
    await flushPromises();
  };

  beforeEach(() => {
    localStorage.clear();
    fetchSource().mockResolvedValue(source());
  });

  afterEach(() => vi.restoreAllMocks());

  it("gives a board moderator the Топик закреплен box under Топик закрыт, set from the topic", async () => {
    signIn({ username: "BoardMod", role: "RegularUser" });
    const wrapper = render({
      topic: moderated({ isAttached: true } as Partial<Topic>),
    });

    await pressEdit(wrapper);

    expect(boxes(wrapper)).toEqual(["Топик закрыт", "Топик закреплен"]);
    expect((pinnedBox(wrapper)!.element as HTMLInputElement).checked).toBe(
      true,
    );
  });

  it("gives an administrator the box on a board he does not moderate", async () => {
    signIn({ username: "Admin1", role: "Admin" });
    const wrapper = render();

    await pressEdit(wrapper);

    expect((pinnedBox(wrapper)!.element as HTMLInputElement).checked).toBe(
      false,
    );
  });

  it("offers the author the closed box and not the pinned one", async () => {
    signIn({ username: "SolohinLex" });
    const submitEdit = accepted();
    const wrapper = render({ topic: moderated(), submitEdit });

    await pressEdit(wrapper);
    expect(boxes(wrapper)).toEqual(["Топик закрыт"]);
    await save(wrapper);

    expect(submitEdit.mock.calls[0][1]).not.toHaveProperty("isAttached");
  });

  it("offers a moderator of the site who does not moderate the board neither box, and sends no pin", async () => {
    signIn({ username: "GlobalMod", role: "SeniorModerator" });
    const submitEdit = accepted();
    const wrapper = render({ topic: moderated(), submitEdit });

    await pressEdit(wrapper);
    expect(wrapper.find(".topic-edit").exists()).toBe(true);
    expect(boxes(wrapper)).toEqual([]);
    await save(wrapper);

    expect(submitEdit.mock.calls[0][1]).not.toHaveProperty("isAttached");
  });

  it("pins the topic in the same save as the title and text", async () => {
    signIn({ username: "BoardMod" });
    const submitEdit = accepted();
    const wrapper = render({ topic: moderated(), submitEdit });

    await pressEdit(wrapper);
    await pinnedBox(wrapper)!.setValue(true);
    await save(wrapper);

    expect(submitEdit).toHaveBeenCalledTimes(1);
    expect(submitEdit).toHaveBeenCalledWith("t-1", {
      title: "Правила",
      description: "[b]Правила[/b]",
      isClosed: false,
      isAttached: true,
    });
    expect(wrapper.find(".topic-edit").exists()).toBe(false);
  });

  it("unpins a pinned topic when the box is cleared", async () => {
    signIn({ username: "BoardMod" });
    const submitEdit = accepted();
    const wrapper = render({
      topic: moderated({ isAttached: true } as Partial<Topic>),
      submitEdit,
    });

    await pressEdit(wrapper);
    await pinnedBox(wrapper)!.setValue(false);
    await save(wrapper);

    expect(submitEdit).toHaveBeenCalledWith(
      "t-1",
      expect.objectContaining({ isAttached: false }),
    );
  });
});

/**
 * A warning is addressed to the author of the topic, and the author of a
 * topic whose account was removed arrives as null. The caption line offered
 * "Предупреждение" there all the same, and the page, finding nobody to warn,
 * did nothing on the press.
 */
describe("TopicView: a warning needs somebody to address", () => {
  const caption = (wrapper: VueWrapper) =>
    wrapper.get(".caption-line").element.textContent ?? "";

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem(
      "user",
      JSON.stringify({ username: "Mod", role: "Moderator" }),
    );
  });

  it("offers a moderator the warning on a topic with an author", () => {
    expect(caption(render())).toContain("Предупреждение");
  });

  // A warning reaches only a rank below the reader's and never an
  // administrator (the server's rank rule, the owner 05.10: "нет").
  it.each(["Moderator", "SeniorModerator", "Admin"])(
    "does not offer it to a moderator on a topic of a %s",
    (role) => {
      const wrapper = render({
        topic: {
          ...topic(),
          author: { username: "Staff", role, lastActivityUtc: null },
        } as unknown as Topic,
      });

      expect(caption(wrapper)).not.toContain("Предупреждение");
    },
  );

  it("does not offer it on a topic whose author's account was removed", () => {
    const wrapper = render({
      topic: { ...topic(), author: null } as unknown as Topic,
    });

    expect(caption(wrapper)).toContain("Аккаунт удален");
    expect(caption(wrapper)).not.toContain("Предупреждение");
  });
});
