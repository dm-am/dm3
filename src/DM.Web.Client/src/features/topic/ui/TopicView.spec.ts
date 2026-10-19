/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia } from "pinia";
import { forumApi } from "@/entities/forum";
import type { Topic } from "@/entities/forum";
import { useToast } from "@/shared/lib/composables/useToast";
import TopicView from "./TopicView.vue";

vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    template: "<textarea class='editor-stub' :value='modelValue' />",
    props: ["modelValue"],
  },
}));

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

const refused = (status: number, title = "") =>
  ({ data: null, error: { type: "", title, status, traceId: "t" } }) as never;

/** The AuthorEdit answer: BBCode, not HTML. */
const source = () =>
  ({
    data: { resource: { ...topic(), description: "[b]Правила[/b]" } },
    error: null,
  }) as never;

const fetchSource = () => vi.spyOn(forumApi, "getTopicForUpdate");

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

const plates = () => useToast().toasts.value.map((t) => t.message);

describe("TopicView edit source", () => {
  beforeEach(() => {
    localStorage.clear();
    // The signed-in author, so the footer offers the edit.
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

const save = async (wrapper: VueWrapper) => {
  await wrapper
    .findAll("button")
    .find((b) => b.text() === "Сохранить")!
    .trigger("click");
  await flushPromises();
};

describe("TopicView: closing lives in the edit form", () => {
  const caption = (wrapper: VueWrapper) =>
    (wrapper.get(".caption-line").element.textContent ?? "")
      .replace(/\s+/g, " ")
      .trim();

  const closedBox = (wrapper: VueWrapper) =>
    wrapper
      .findAll("label.checkbox-label")
      .find((l) => l.text() === "Топик закрыт")
      ?.find("input[type='checkbox']");

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

  // A removed author and a guest are not the same person.
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

  it("offers the author neither the form nor the box on a topic a moderator closed", () => {
    signIn({ username: "SolohinLex" });
    const wrapper = render({ topic: moderated({ isClosed: true }) });

    expect(editButton(wrapper)).toBeUndefined();
    expect(closedBox(wrapper)).toBeUndefined();
    expect(caption(wrapper)).toContain("Удалить");
  });

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
    expect(wrapper.get(".topic-edit .action-result").text()).toBe(
      "Топик не найден",
    );
    expect(plates()).toEqual([]);
  });
});

describe("TopicView: pinning lives in the edit form", () => {
  const boxes = (wrapper: VueWrapper) =>
    wrapper.findAll("label.checkbox-label").map((label) => label.text());

  const pinnedBox = (wrapper: VueWrapper) =>
    wrapper
      .findAll("label.checkbox-label")
      .find((l) => l.text() === "Топик закреплен")
      ?.find("input[type='checkbox']");

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
