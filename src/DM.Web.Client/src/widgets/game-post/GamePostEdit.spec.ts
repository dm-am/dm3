/**
 * @vitest-environment jsdom
 */

/** The edit of a published post: its seed, its save and its refusal. */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { postApi } from "@/entities/game";
import type { Post } from "@/entities/game";
import { uploadApi } from "@/shared/api";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  copied,
  lineOf,
  post,
  render,
  renderAs,
  ROOM,
  roomPost,
  stubResizeObserver,
  type PostWrapper,
} from "./GamePost.fixtures";

enableAutoUnmount(afterEach);

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

stubResizeObserver();

/** The editor is seeded from the author's own text as typed, the only one that keeps [private]. */
describe("GamePost seeds the editor from the author's own text", () => {
  // What the page shows: nothing in it can produce a [private] tag.
  const DISPLAY_HTML =
    '<strong>Жирно</strong> <div class="private-message">Секрет</div>' +
    '<div class="private-message-header">Получатели: Чак</div> хвост';

  // The author's own text as typed, the author_edit answer.
  const AUTHOR_SOURCE =
    '[b]Жирно[/b] [private="Чак"]Секрет[/private] хвост, Том &amp; Джерри';

  const ownPost = {
    ...post,
    gameText: DISPLAY_HTML,
    // Inside the fifteen minutes, so the button is drawn for its author.
    createdUtc: new Date().toISOString(),
  } as unknown as Post;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: [] },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  /** Mounts the post as its own author sees it on the room page. */
  const renderOwn = () => renderAs("Author", { gameText: DISPLAY_HTML });

  const editButton = (wrapper: PostWrapper) =>
    wrapper
      .findAll("button")
      .find((b) => b.text() === "Редактировать") as ReturnType<
      typeof wrapper.get
    >;

  const seededText = (wrapper: PostWrapper) =>
    wrapper.get(".post-edit .editor-stub").attributes("value") ?? "";

  it("takes the seed out of the envelope, not off its lid", async () => {
    vi.spyOn(postApi, "getPostForEdit").mockResolvedValue({
      data: { resource: { ...ownPost, gameText: AUTHOR_SOURCE } },
      error: undefined,
    } as never);

    const wrapper = await renderOwn();
    await editButton(wrapper).trigger("click");
    await flushPromises();

    // The answer as it came: not the display rendering, whose addressee line is prose.
    expect(seededText(wrapper)).toBe(AUTHOR_SOURCE);
  });

  // The display text in the editor would be saved back as BBCode, its markup as text.
  it("opens no editor on an answer that carries no post, and says so", async () => {
    vi.spyOn(postApi, "getPostForEdit").mockResolvedValue({
      data: { resource: null },
      error: undefined,
    } as never);

    const wrapper = await renderOwn();
    await editButton(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.find(".post-edit").exists()).toBe(false);
    expect(wrapper.get(".post-edit-line + .action-result").text()).toBe(
      "Не удалось загрузить текст поста",
    );
  });

  it("opens no editor on a refused read, and says so", async () => {
    vi.spyOn(postApi, "getPostForEdit").mockResolvedValue({
      data: undefined,
      error: { status: 500, title: "", detail: "", type: "", traceId: "" },
    } as never);

    const wrapper = await renderOwn();
    await editButton(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.find(".post-edit").exists()).toBe(false);
    expect(wrapper.get(".post-edit-line + .action-result").text()).toBe(
      "Не удалось загрузить текст поста",
    );
  });

  it('holds "Редактировать" busy until the source arrives', async () => {
    let answer!: (value: unknown) => void;
    const read = vi
      .spyOn(postApi, "getPostForEdit")
      .mockReturnValue(new Promise((resolve) => (answer = resolve)) as never);

    const wrapper = await renderOwn();
    await editButton(wrapper).trigger("click");
    await editButton(wrapper).trigger("click");

    expect(wrapper.find(".post-edit").exists()).toBe(false);
    expect(editButton(wrapper).attributes("disabled")).toBeDefined();
    expect(editButton(wrapper).attributes("aria-busy")).toBe("true");
    expect(read).toHaveBeenCalledTimes(1);

    answer({ data: { resource: { ...ownPost, gameText: AUTHOR_SOURCE } } });
    await flushPromises();
    expect(seededText(wrapper)).toBe(AUTHOR_SOURCE);
  });

  // The metagame text is checked as the comment surface it is saved on.
  it("checks the metagame text as the surface it is saved on", async () => {
    vi.spyOn(postApi, "getPostForEdit").mockResolvedValue({
      data: { resource: ownPost },
      error: undefined,
    } as never);

    const wrapper = await renderOwn();
    await editButton(wrapper).trigger("click");
    await flushPromises();

    expect(
      wrapper
        .findAllComponents({ name: "BBCodeEditor" })
        .map((editor) => [
          editor.props("context"),
          editor.props("checkContext"),
        ]),
    ).toEqual([
      ["post", undefined],
      ["post", "common"],
    ]);
  });
});

/** A save dates the edit line by the edit; a refusal stands under the edit's buttons. */
describe("GamePost edit line", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: [] },
      error: undefined,
    } as never);
    vi.spyOn(postApi, "getPostForEdit").mockResolvedValue({
      data: { resource: { ...roomPost, gameText: "[b]Текст поста[/b]" } },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  /** The answer to a save that the server took. */
  const savedEdit = () => ({
    data: {
      resource: {
        ...roomPost,
        gameText: "<p>Новый текст</p>",
        edits: [{ id: "e-2", modifiedUtc: "2026-07-27T09:05:00" }],
      },
    },
    error: undefined,
  });

  /** The author's post with its edit opened and "Сохранить" pressed. */
  async function saveAnEdit() {
    const wrapper = await renderAs("Author");
    const edit = wrapper
      .findAll(".post-edit-line .caption-action")
      .find((b) => b.text() === "Редактировать")!;
    await edit.trigger("click");
    await flushPromises();
    await wrapper.get(".edit-btn.save").trigger("click");
    return wrapper;
  }

  // Stored as the author passes it: no trim on the way, and an empty metagame
  // text goes, which is how the edit erases it.
  it("sends both texts as they stand in the editor", async () => {
    vi.mocked(postApi.getPostForEdit).mockResolvedValue({
      data: {
        resource: {
          ...roomPost,
          gameText: "  [B]Текст поста[/B]\n",
          metagameText: "",
        },
      },
      error: undefined,
    } as never);
    const update = vi
      .spyOn(postApi, "updatePost")
      .mockResolvedValue(savedEdit() as never);
    const wrapper = await saveAnEdit();
    await flushPromises();

    expect(update.mock.calls[0][1]).toEqual({
      gameText: "  [B]Текст поста[/B]\n",
      metagameText: "",
    });
    wrapper.unmount();
  });

  it("dates a fresh edit by the edit itself", async () => {
    vi.spyOn(postApi, "updatePost").mockResolvedValue(savedEdit() as never);
    const wrapper = await saveAnEdit();
    await flushPromises();

    expect(copied(lineOf(wrapper).element)).toBe(
      "Отредактировано 27.07.2026 в 09:05 | Редактировать | Удалить",
    );
    wrapper.unmount();
  });

  // "Отмена" during a save closes the edit; the server keeps it, so the post shows it.
  it("keeps an edit saved behind a cancelled edit on the post", async () => {
    let answer!: (value: unknown) => void;
    vi.spyOn(postApi, "updatePost").mockReturnValue(
      new Promise((resolve) => (answer = resolve)) as never,
    );
    const wrapper = await saveAnEdit();
    const cancel = wrapper
      .findAll(".edit-btn")
      .find((b) => !b.classes().includes("save"))!;
    await cancel.trigger("click");
    expect(wrapper.find(".post-edit").exists()).toBe(false);

    answer(savedEdit());
    await flushPromises();

    expect(wrapper.html()).toContain("Новый текст");
    wrapper.unmount();
  });

  it("names the text the server refused by the label over its editor", async () => {
    const toast = useToast();
    for (const standing of toast.toasts.value) toast.dismiss(standing.id);
    vi.spyOn(postApi, "updatePost").mockResolvedValue({
      data: undefined,
      error: {
        status: 400,
        title: "",
        detail: "",
        type: "",
        traceId: "",
        errors: { GameText: ["Invalid"] },
      },
    } as never);
    const wrapper = await saveAnEdit();
    await flushPromises();

    // Under the buttons of the edit, which stays open; no plate.
    expect(wrapper.get(".edit-actions + .action-result").text()).toBe(
      "Игровой текст: некорректное значение",
    );
    expect(toast.toasts.value.map((t) => t.message)).toEqual([]);
    for (const standing of toast.toasts.value) toast.dismiss(standing.id);
    wrapper.unmount();
  });
});

/** What may not be done is not offered: the server says who may edit a post not his own. */
describe("GamePost edit of a post not one's own", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: [] },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const buttons = (wrapper: PostWrapper) =>
    lineOf(wrapper)
      .findAll(".caption-action")
      .map((b) => b.text());

  // Nothing in a game room is moderated: a moderator neither deletes a post of somebody else nor
  // edits it, unless the server lets him as a lead of the game, who reads every hidden block.
  it("offers a moderator nothing on a post not his own, but the edit the server lets him make", async () => {
    const closed = await renderAs(
      "Moder",
      { viewer: { canReview: false, canEdit: false } },
      true,
      "Moderator",
    );
    expect(closed.text()).not.toContain("Удалить");
    expect(closed.text()).not.toContain("Редактировать");

    const open = await renderAs(
      "Moder",
      { viewer: { canReview: false, canEdit: true } },
      true,
      "Moderator",
    );
    expect(buttons(open)).toEqual(["Редактировать"]);
  });
});

/** A file chosen in the edit goes with "Сохранить", with the mark chosen for it. */
describe("GamePost files of an edit", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: [] },
      error: undefined,
    } as never);
    vi.spyOn(postApi, "getPostForEdit").mockResolvedValue({
      data: { resource: { ...roomPost, gameText: "[b]Текст поста[/b]" } },
      error: undefined,
    } as never);
    vi.spyOn(postApi, "updatePost").mockResolvedValue({
      data: { resource: { ...roomPost, gameText: "<p>Новый текст</p>" } },
      error: undefined,
    } as never);
    vi.spyOn(postApi, "getPost").mockResolvedValue({
      data: { resource: { ...roomPost, attachments: [] } },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // A room with one character of another player: the one box of the panel.
  const room = {
    accesses: [
      {
        id: "access-vasya",
        character: {
          id: "c-vasya",
          name: "Вася",
          isNpc: false,
          author: { username: "vasya" },
        },
      },
    ],
  };

  async function openEditWithFile(name: string) {
    const wrapper = await render(
      ROOM,
      { ...roomPost, createdUtc: new Date().toISOString() } as Post,
      { viewer: "Author", props: { editable: true, room } },
    );
    await lineOf(wrapper)
      .findAll(".caption-action")
      .find((b) => b.text() === "Редактировать")!
      .trigger("click");
    await flushPromises();
    const input = wrapper.get(".attachment-input");
    Object.defineProperty(input.element, "files", {
      value: [new File(["x"], name, { type: "image/png" })],
    });
    await input.trigger("change");
    return wrapper;
  }

  it("sends the file only with Сохранить, marked as chosen", async () => {
    const upload = vi
      .spyOn(uploadApi, "directUpload")
      .mockResolvedValue({ data: { id: "u-1" }, error: null } as never);
    const wrapper = await openEditWithFile("map.png");

    await wrapper.get(".post-edit .attachment-audience").trigger("click");
    await wrapper.get("[role=dialog] input").setValue(true);
    await wrapper
      .findAll("[role=dialog] button")
      .find((b) => b.text() === "Готово")!
      .trigger("click");
    expect(wrapper.get(".post-edit .attachment-audience").text()).toBe(
      "видят: Вася",
    );
    expect(upload).not.toHaveBeenCalled();

    await wrapper.get(".edit-btn.save").trigger("click");
    await flushPromises();

    expect(upload).toHaveBeenCalledTimes(1);
    expect(upload.mock.calls[0][2]).toEqual({
      targetId: roomPost.id,
      audience: { visibleTo: "Selected", characterIds: ["c-vasya"] },
    });
    expect(wrapper.find(".post-edit").exists()).toBe(false);
  });

  it("sends nothing when the edit is cancelled", async () => {
    const upload = vi.spyOn(uploadApi, "directUpload");
    const wrapper = await openEditWithFile("map.png");

    await wrapper
      .findAll(".edit-btn")
      .find((b) => !b.classes().includes("save"))!
      .trigger("click");
    await flushPromises();

    expect(upload).not.toHaveBeenCalled();
  });

  it("says a file the saved edit failed to attach under the edit line", async () => {
    vi.spyOn(uploadApi, "directUpload").mockResolvedValue({
      data: null,
      error: { status: 400, title: "Нет" },
    } as never);
    const wrapper = await openEditWithFile("map.png");

    await wrapper.get(".edit-btn.save").trigger("click");
    await flushPromises();

    expect(wrapper.get(".post-edit-refusal").text()).toBe(
      "Пост сохранен, но файлы не приложились: map.png. Приложите их через правку поста.",
    );
  });
});
