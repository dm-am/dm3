/**
 * @vitest-environment jsdom
 */

/**
 * The personal, game and blog notepads were three copies of this screen and
 * they drifted: only one asked before deleting, only one had a skeleton, and
 * each spelled its own toasts. These tests pin the behaviour the single copy
 * now owes all three — the delete gate above all, since a native confirm()
 * would pass a "does it delete" test just as happily.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import Button from "@/shared/ui/Button/Button.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import NotepadBoard from "./NotepadBoard.vue";
import type { NotepadAdapter } from "./types";
import type { NotepadEntry } from "@/shared/api/models/notepads";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";

const entry = (over: Partial<NotepadEntry> = {}): NotepadEntry => ({
  id: "1",
  notepadType: "User",
  containerId: "c",
  authorId: "u",
  title: "Первая",
  content: "Текст",
  sortOrder: 0,
  createdUtc: "2026-07-01T10:00:00Z",
  modifiedUtc: null,
  ...over,
});

const adapterOf = (entries: NotepadEntry[]): NotepadAdapter => ({
  list: vi.fn().mockResolvedValue({ data: { resources: entries } }),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn().mockResolvedValue({}),
});

const toast = useToast();

/** What is on screen in toasts right now, in the order it arrived. */
const said = (): string[] => toast.toasts.value.map((t) => t.message);

beforeEach(() => {
  // The toast list is module state shared by every test in the run.
  for (const item of [...toast.toasts.value]) toast.dismiss(item.id);
});

const mountBoard = (adapter: NotepadAdapter, props = {}) =>
  mount(NotepadBoard, {
    // Attached: the keyboard tests below assert on document.activeElement,
    // and a detached tree has no focus to move.
    attachTo: document.body,
    // The real Button, registered globally by the app: it is what saves the
    // editor, and a stub would swallow the click these tests are made of.
    global: { plugins: [createPinia()], components: { Button } },
    props: {
      adapter,
      title: "Блокнот",
      // "u" is who wrote every fixture entry, so the default board is the
      // ordinary case: a viewer reading their own notes.
      viewerId: "u",
      emptyText: "Нет записей в блокноте",
      emptyHint: "Создайте первую запись.",
      loadErrorText: "Не удалось загрузить блокнот",
      ...props,
    },
  });

/** The action of the entry's caption line with this caption, if offered. */
const action = (wrapper: ReturnType<typeof mountBoard>, caption: string) =>
  wrapper
    .findAll(".content-meta .caption-action")
    .find((button) => button.text() === caption);

/** The captions of the actions the entry's caption line offers. */
const actions = (wrapper: ReturnType<typeof mountBoard>) =>
  wrapper.findAll(".content-meta .caption-action").map((b) => b.text());

describe("NotepadBoard", () => {
  it("does not touch the endpoints when the viewer has no access", async () => {
    const adapter = adapterOf([]);
    const wrapper = mountBoard(adapter, {
      accessible: false,
      deniedText: "Блокнот доступен только мастеру",
    });
    await flushPromises();

    expect(adapter.list).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Блокнот доступен только мастеру");
  });

  it("loads once access is granted after mount", async () => {
    // The gate comes from a container store that may still be loading when
    // the tab mounts; without this the notepad stays empty forever.
    const adapter = adapterOf([entry()]);
    const wrapper = mountBoard(adapter, { accessible: false });
    await flushPromises();
    expect(adapter.list).not.toHaveBeenCalled();

    await wrapper.setProps({ accessible: true });
    await flushPromises();

    expect(adapter.list).toHaveBeenCalledTimes(1);
  });

  it("orders the list by sortOrder, not by arrival", async () => {
    const wrapper = mountBoard(
      adapterOf([
        entry({ id: "2", title: "Вторая", sortOrder: 2 }),
        entry({ id: "1", title: "Первая", sortOrder: 1 }),
      ]),
    );
    await flushPromises();

    const titles = wrapper.findAll(".entry-title").map((n) => n.text());
    expect(titles).toEqual(["Первая", "Вторая"]);
  });

  it("asks through the dialog before deleting, and only then calls remove", async () => {
    const adapter = adapterOf([entry()]);
    const wrapper = mountBoard(adapter);
    await flushPromises();

    await wrapper.find(".entry-item").trigger("click");
    await action(wrapper, "Удалить")!.trigger("click");

    // The dialog is teleported to body, so assert on the document.
    expect(document.body.textContent).toContain("Удалить запись?");
    expect(document.body.textContent).toContain('Запись "Первая"');
    expect(adapter.remove).not.toHaveBeenCalled();

    const confirmButton =
      document.body.querySelector<HTMLButtonElement>(".dialog-btn-submit");
    confirmButton?.click();
    await flushPromises();

    // The question prints a refusal in itself, so the request tells the
    // interceptor to keep quiet about it.
    expect(adapter.remove).toHaveBeenCalledWith("1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.text()).toContain("Нет записей в блокноте");
    expect(document.body.textContent).not.toContain("Удалить запись?");
    wrapper.unmount();
  });

  it("keeps the question open with a refusal in it, and the entry in the list", async () => {
    const adapter = adapterOf([entry()]);
    vi.mocked(adapter.remove).mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    });
    const wrapper = mountBoard(adapter);
    await flushPromises();

    await wrapper.find(".entry-item").trigger("click");
    await action(wrapper, "Удалить")!.trigger("click");
    document.body
      .querySelector<HTMLButtonElement>(".dialog-btn-submit")
      ?.click();
    await flushPromises();

    expect(
      document.body.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Не удалось удалить запись");
    expect(document.body.textContent).toContain("Удалить запись?");
    expect(said()).toEqual([]);
    expect(wrapper.findAll(".entry-title").map((n) => n.text())).toEqual([
      "Первая",
    ]);
    wrapper.unmount();
  });

  // The list was a bare div with @click, and the reader pane renders only for
  // a selected entry — so the whole screen was mouse-only on all three notepad
  // pages. These pin the listbox contract that replaced it.
  describe("entry list is an operable listbox", () => {
    const threeEntries = () => [
      entry({ id: "1", title: "Первая", sortOrder: 1 }),
      entry({ id: "2", title: "Вторая", sortOrder: 2 }),
      entry({ id: "3", title: "Третья", sortOrder: 3 }),
    ];

    const mountList = async () => {
      const wrapper = mountBoard(adapterOf(threeEntries()));
      await flushPromises();
      // Shared precondition: every test below drives the list through its
      // options, and asserting it here fails with "the rows are not options"
      // instead of "undefined has no trigger".
      expect(wrapper.findAll('[role="option"]')).toHaveLength(3);
      return wrapper;
    };

    const options = (wrapper: Awaited<ReturnType<typeof mountList>>) =>
      wrapper.findAll('[role="option"]');

    it("announces the list as a listbox of options", async () => {
      const wrapper = await mountList();

      const list = wrapper.find(".entry-list");
      expect(list.attributes("role")).toBe("listbox");
      expect(list.attributes("aria-label")).toBe("Блокнот");
      expect(
        options(wrapper).every((o) => o.classes().includes("entry-item")),
      ).toBe(true);

      wrapper.unmount();
    });

    it("keeps exactly one option in the Tab cycle, on the selection", async () => {
      const wrapper = await mountList();
      const tabIndexes = () =>
        options(wrapper).map((o) => o.attributes("tabindex"));

      // Nothing is selected on arrival, so the tab stop sits on the first
      // entry — otherwise Tab would walk past the list entirely.
      expect(tabIndexes()).toEqual(["0", "-1", "-1"]);

      await options(wrapper)[2].trigger("click");
      expect(tabIndexes()).toEqual(["-1", "-1", "0"]);
      expect(
        options(wrapper).map((o) => o.attributes("aria-selected")),
      ).toEqual(["false", "false", "true"]);

      wrapper.unmount();
    });

    it("opens an entry on Enter", async () => {
      const wrapper = await mountList();
      expect(wrapper.find(".no-selection").exists()).toBe(true);

      await options(wrapper)[0].trigger("keydown", { key: "Enter" });

      expect(wrapper.find(".content-title").text()).toBe("Первая");
      expect(wrapper.find(".content-body").text()).toBe("Текст");

      wrapper.unmount();
    });

    it("opens an entry on Space", async () => {
      const wrapper = await mountList();

      await options(wrapper)[1].trigger("keydown", { key: " " });

      expect(wrapper.find(".content-title").text()).toBe("Вторая");

      wrapper.unmount();
    });

    it("walks the list with the arrows, Home and End, selection following focus", async () => {
      const wrapper = await mountList();
      const items = options(wrapper);
      const openTitle = () => wrapper.find(".content-title").text();

      await items[0].trigger("keydown", { key: "ArrowDown" });
      expect(openTitle()).toBe("Вторая");
      expect(document.activeElement).toBe(items[1].element);

      await items[1].trigger("keydown", { key: "End" });
      expect(openTitle()).toBe("Третья");
      expect(document.activeElement).toBe(items[2].element);

      // Wraps at both ends.
      await items[2].trigger("keydown", { key: "ArrowDown" });
      expect(openTitle()).toBe("Первая");
      expect(document.activeElement).toBe(items[0].element);

      await items[0].trigger("keydown", { key: "ArrowUp" });
      expect(openTitle()).toBe("Третья");
      expect(document.activeElement).toBe(items[2].element);

      await items[2].trigger("keydown", { key: "Home" });
      expect(openTitle()).toBe("Первая");
      expect(document.activeElement).toBe(items[0].element);

      wrapper.unmount();
    });
  });

  /**
   * Creating an entry used to leave the reader pane on "Выберите запись для
   * просмотра" — the very screen "Отмена" leaves behind — and a toast was
   * asked to tell the two apart. The entry the create endpoint answers with
   * (Envelope<NotepadEntryResponse>, the same DTO the list serves) is now what
   * the pane shows, so the screen tells them apart by itself.
   */
  describe("a created entry is what the reader is left looking at", () => {
    const first = () => entry({ id: "1", title: "Первая", sortOrder: 1 });

    const made = (): NotepadEntry =>
      entry({
        id: "2",
        title: "Свежая",
        content: "Только что написано",
        sortOrder: 2,
      });

    /** An adapter whose create answers with the entry the server made. */
    const adapterCreating = (resource: NotepadEntry) => ({
      ...adapterOf([first()]),
      create: vi.fn().mockResolvedValue({ data: { resource }, error: null }),
    });

    /** Open the editor and write an entry into it, stopping before the save. */
    const write = async (adapter: NotepadAdapter) => {
      const wrapper = mountBoard(adapter);
      await flushPromises();
      await wrapper.find(".add-btn").trigger("click");
      await wrapper.find("#entry-title").setValue("Свежая");
      await wrapper.find("#entry-content").setValue("Только что написано");
      return wrapper;
    };

    /** The save control: the one button in the footer that is not "Отмена". */
    const save = async (wrapper: ReturnType<typeof mountBoard>) => {
      await wrapper
        .find(".editor-actions button:not(.cancel-btn)")
        .trigger("click");
      await flushPromises();
    };

    it("opens the created entry, where a cancel leaves the placeholder", async () => {
      const adapter = adapterCreating(made());
      const wrapper = await write(adapter);

      // The other ending of the same editor, for contrast: nothing was
      // written, so there is nothing to show and the placeholder is right.
      await wrapper.find(".cancel-btn").trigger("click");
      expect(wrapper.find(".no-selection").text()).toContain(
        "Выберите запись для просмотра",
      );

      const again = await write(adapterCreating(made()));
      await save(again);

      expect(again.find(".no-selection").exists()).toBe(false);
      expect(again.find(".content-title").text()).toBe("Свежая");
      expect(again.find(".content-body").text()).toBe("Только что написано");

      wrapper.unmount();
      again.unmount();
    });

    it("moves the selection in the list onto it, so both panes agree", async () => {
      const wrapper = await write(adapterCreating(made()));
      await save(wrapper);

      const options = wrapper.findAll('[role="option"]');
      expect(wrapper.findAll(".entry-title").map((n) => n.text())).toEqual([
        "Первая",
        "Свежая",
      ]);
      expect(options.map((o) => o.attributes("aria-selected"))).toEqual([
        "false",
        "true",
      ]);
      // Roving tabindex follows the selection: Tab now lands on the new entry.
      expect(options.map((o) => o.attributes("tabindex"))).toEqual(["-1", "0"]);

      wrapper.unmount();
    });

    it("says nothing in a toast about what the screen has just shown", async () => {
      const wrapper = await write(adapterCreating(made()));
      await save(wrapper);

      expect(said()).toEqual([]);

      wrapper.unmount();
    });
  });

  /**
   * A refused save leaves the editor standing with the text still in it, and
   * the reader looking at that editor. It used to be answered by a toast in
   * the far corner - raised directly, not through notifyFailure, so on a 403,
   * 429 or 5xx it stood beside the interceptor's plate: one press, two red
   * boxes saying the same thing. The refusal now goes where the eye is, in the
   * form's own line (p.form-error, the shape features/edit-character prints),
   * and only for the statuses the interceptor does not announce itself.
   */
  describe("a refused save is answered inside the editor", () => {
    const made = (): NotepadEntry =>
      entry({ id: "2", title: "Свежая", content: "Написано", sortOrder: 2 });

    /** An adapter whose create answers the way the wire does: data or error. */
    const adapterAnswering = (
      ...answers: { data: object | null; error: object | null }[]
    ): NotepadAdapter => {
      const create = vi.fn();
      for (const answer of answers) create.mockResolvedValueOnce(answer);
      return { ...adapterOf([entry()]), create };
    };

    const refusal = (status: number, title = "") => ({
      data: null,
      error: { type: "Failure", status, title, traceId: "t" },
    });

    /** Open the editor and write an entry into it, stopping before the save. */
    const write = async (adapter: NotepadAdapter) => {
      const wrapper = mountBoard(adapter);
      await flushPromises();
      await wrapper.find(".add-btn").trigger("click");
      await wrapper.find("#entry-title").setValue("Свежая");
      await wrapper.find("#entry-content").setValue("Написано");
      return wrapper;
    };

    /** The save control: the one button in the footer that is not "Отмена". */
    const save = async (wrapper: ReturnType<typeof mountBoard>) => {
      await wrapper
        .find(".editor-actions button:not(.cancel-btn)")
        .trigger("click");
      await flushPromises();
    };

    /** The refusal line, looked for inside the editor and nowhere else. */
    const line = (wrapper: ReturnType<typeof mountBoard>) =>
      wrapper.find(".editor .form-error");

    const typedTitle = (wrapper: ReturnType<typeof mountBoard>) =>
      (wrapper.find("#entry-title").element as HTMLInputElement).value;

    it("prints the server's reason in the form, and nothing in the corner", async () => {
      const wrapper = await write(
        adapterAnswering(refusal(400, "Название слишком длинное")),
      );
      await save(wrapper);

      expect(line(wrapper).exists()).toBe(true);
      expect(line(wrapper).text()).toBe("Название слишком длинное");
      expect(line(wrapper).attributes("role")).toBe("alert");
      expect(said()).toEqual([]);
      // Nothing was saved, and the editor still holds what was written.
      expect(wrapper.find(".editor").exists()).toBe(true);
      expect(typedTitle(wrapper)).toBe("Свежая");

      wrapper.unmount();
    });

    it("falls back to its own sentence when the server named nothing", async () => {
      const wrapper = await write(adapterAnswering(refusal(404)));
      await save(wrapper);

      expect(line(wrapper).text()).toBe("Не удалось сохранить запись");
      expect(said()).toEqual([]);

      wrapper.unmount();
    });

    it("says so when the answer carries no entry to show", async () => {
      // A 2xx with nothing in it: no problem document, so no status to
      // consult, and no entry for the pane to open.
      const wrapper = await write(adapterAnswering({ data: {}, error: null }));
      await save(wrapper);

      expect(line(wrapper).text()).toBe("Не удалось сохранить запись");
      expect(wrapper.find(".editor").exists()).toBe(true);
      expect(said()).toEqual([]);

      wrapper.unmount();
    });

    /**
     * The statuses the response interceptor used to plate stand in the editor's
     * line now, in the server's own words, and the plate is taken down
     * (refusalHere). An expired session and a dead connection keep the plate.
     */
    it.each([
      [403, "Ошибка сервера"],
      [429, "Слишком много попыток, попробуйте позже"],
      [500, "Ошибка сервера"],
      [503, "Ошибка сервера"],
    ])("says a refusal of status %i in the editor", async (status, text) => {
      const wrapper = await write(
        adapterAnswering(refusal(status, "Ошибка сервера")),
      );
      await save(wrapper);

      expect(line(wrapper).text()).toBe(text);
      expect(said()).toEqual([]);
      expect(wrapper.find(".editor").exists()).toBe(true);
      expect(typedTitle(wrapper)).toBe("Свежая");

      wrapper.unmount();
    });

    it.each([0, 401])(
      "leaves a refusal of status %i to the interceptor",
      async (status) => {
        const wrapper = await write(
          adapterAnswering(refusal(status, "Ошибка сервера")),
        );
        await save(wrapper);

        expect(line(wrapper).exists()).toBe(false);
        expect(said()).toEqual([]);
        // Silent, but not closed: nothing was saved, and the editor still
        // holds everything that was typed into it.
        expect(wrapper.find(".editor").exists()).toBe(true);
        expect(typedTitle(wrapper)).toBe("Свежая");

        wrapper.unmount();
      },
    );

    it("takes the line off on the next attempt", async () => {
      const wrapper = await write(
        adapterAnswering(refusal(404), {
          data: { resource: made() },
          error: null,
        }),
      );
      await save(wrapper);
      expect(line(wrapper).exists()).toBe(true);

      await save(wrapper);

      // The second attempt went through: the editor is gone, the line with
      // it, and the pane shows what was saved.
      expect(wrapper.find(".editor").exists()).toBe(false);
      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(wrapper.find(".content-title").text()).toBe("Свежая");

      wrapper.unmount();
    });

    it("opens the next editor with no refusal left in it", async () => {
      const wrapper = await write(adapterAnswering(refusal(404)));
      await save(wrapper);
      expect(line(wrapper).exists()).toBe(true);

      await wrapper.find(".cancel-btn").trigger("click");
      await wrapper.find(".add-btn").trigger("click");

      expect(line(wrapper).exists()).toBe(false);

      wrapper.unmount();
    });
  });

  /**
   * An entry with no title is the client's own verdict, reached before
   * anything is sent: the reader is standing in the editor with the cursor in
   * the very field the verdict is about. It used to be delivered by a toast in
   * the opposite corner of the screen, while every other field on the site
   * answers with a red line under itself - the one the shared FormField prints
   * (shared/ui/Form/FormField.vue), which is why the line here carries its
   * class names.
   */
  describe("an untitled entry is refused under the field", () => {
    const openEditor = async () => {
      const adapter = adapterOf([entry()]);
      const wrapper = mountBoard(adapter);
      await flushPromises();
      await wrapper.find(".add-btn").trigger("click");
      return { adapter, wrapper };
    };

    /** The save control: the one button in the footer that is not "Отмена". */
    const trySave = async (wrapper: ReturnType<typeof mountBoard>) => {
      await wrapper
        .find(".editor-actions button:not(.cancel-btn)")
        .trigger("click");
      await flushPromises();
    };

    it("prints the reason under the field and nothing in the corner", async () => {
      const { adapter, wrapper } = await openEditor();

      await trySave(wrapper);

      const line = wrapper.find(".form-field-error");
      expect(line.exists()).toBe(true);
      expect(line.text()).toBe("Введите название записи");
      expect(said()).toEqual([]);
      // Nothing was asked of the server, and the editor is still standing with
      // whatever was written in it.
      expect(adapter.create).not.toHaveBeenCalled();
      expect(wrapper.find(".editor").exists()).toBe(true);

      wrapper.unmount();
    });

    it("points the field at the line, so the two are read together", async () => {
      const { wrapper } = await openEditor();
      await trySave(wrapper);

      const field = wrapper.find("#entry-title");
      expect(field.attributes("aria-invalid")).toBe("true");
      expect(field.attributes("aria-describedby")).toBe(
        wrapper.find(".form-field-error").attributes("id"),
      );
      // The frame of the field turns with the line: colour is not the only
      // carrier, but it is the one that draws the eye to the right field.
      expect(wrapper.find(".form-field").classes()).toContain("error");

      wrapper.unmount();
    });

    it("takes the line off on the first character typed", async () => {
      const { wrapper } = await openEditor();
      await trySave(wrapper);
      expect(wrapper.find(".form-field-error").exists()).toBe(true);

      await wrapper.find("#entry-title").setValue("Свежая");

      expect(wrapper.find(".form-field-error").exists()).toBe(false);
      expect(
        wrapper.find("#entry-title").attributes("aria-invalid"),
      ).toBeUndefined();

      wrapper.unmount();
    });

    it("opens the next editor with no refusal left in it", async () => {
      const { wrapper } = await openEditor();
      await trySave(wrapper);

      await wrapper.find(".cancel-btn").trigger("click");
      await wrapper.find(".add-btn").trigger("click");

      expect(wrapper.find(".form-field-error").exists()).toBe(false);

      wrapper.unmount();
    });
  });

  // One main button to a form (UI_STANDARDS): with the editor open the screen
  // had two, "Новая запись" over the board and "Сохранить" under the editor,
  // and the one that saves nothing was as loud as the one that does.
  it("keeps one main button on screen while the editor is open", async () => {
    const wrapper = mountBoard(adapterOf([entry()]));
    await flushPromises();

    await wrapper.find(".add-btn").trigger("click");

    expect(wrapper.findAll("button.primary").map((b) => b.text())).toEqual([
      "Сохранить",
    ]);
  });

  it("reports a failed load instead of showing an empty notepad", async () => {
    const adapter: NotepadAdapter = {
      ...adapterOf([]),
      list: vi.fn().mockResolvedValue({ error: { title: "нет" } }),
    };
    const wrapper = mountBoard(adapter);
    await flushPromises();

    expect(wrapper.find(".notepad-layout").exists()).toBe(false);
    // In the place of the list, in the server's words, with the way to ask
    // again - and not "Нет записей" under a plate in the corner.
    expect(wrapper.get(".error-state").text()).toContain("нет");
    expect(wrapper.text()).not.toContain("Нет записей в блокноте");
    expect(toast.toasts.value).toEqual([]);

    wrapper.unmount();
  });

  it("falls back on its own sentence for a load the server named nothing about, and asks again", async () => {
    const list = vi
      .fn()
      .mockResolvedValueOnce({
        data: null,
        error: { type: "", title: "", status: 500, traceId: "t" },
      })
      .mockResolvedValueOnce({ data: { resources: [entry()] }, error: null });
    const wrapper = mountBoard({ ...adapterOf([]), list });
    await flushPromises();

    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить блокнот",
    );

    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.find(".notepad-layout").exists()).toBe(true);
    wrapper.unmount();
  });

  /**
   * The board offered "Редактировать" and "Удалить" on every entry it showed,
   * while the API grants the first to the author alone and the second to the
   * author and the lead of the container. Reading somebody else's entry in a
   * shared notepad — the notes of a game, the notes kept about a character,
   * the notes of a blog — both buttons were there and both answered 403.
   */
  describe("the controls over one entry repeat the server's rules", () => {
    const openSomebodyElses = async (props = {}) => {
      const wrapper = mountBoard(
        adapterOf([entry({ authorId: "somebody-else" })]),
        props,
      );
      await flushPromises();
      await wrapper.find(".entry-item").trigger("click");
      return wrapper;
    };

    it("offers both controls on an entry the viewer wrote", async () => {
      const wrapper = mountBoard(adapterOf([entry({ authorId: "u" })]));
      await flushPromises();
      await wrapper.find(".entry-item").trigger("click");

      expect(actions(wrapper)).toEqual(["Редактировать", "Удалить"]);
    });

    it("draws them as words of the entry's caption line, not as buttons by the title", async () => {
      const wrapper = mountBoard(
        adapterOf([entry({ modifiedUtc: "2026-07-02T10:00:00Z" })]),
      );
      await flushPromises();
      await wrapper.find(".entry-item").trigger("click");

      // A long title used to push the buttons beside it out of the pane.
      const line = wrapper.get(".content-meta").text().replace(/\s+/g, " ");
      expect(line).toMatch(
        /^Создано: \S.* \| Изменено: \S.* \| Редактировать \| Удалить$/,
      );
      expect(wrapper.find(".content-title button").exists()).toBe(false);
    });

    it("offers neither on somebody else's entry", async () => {
      const wrapper = await openSomebodyElses();

      // The entry itself still reads — a shared notepad is shared.
      expect(wrapper.find(".content-body").text()).toBe("Текст");
      expect(actions(wrapper)).toEqual([]);
      // The caption alone, with no bar left over from the missing actions.
      expect(wrapper.get(".content-meta").text()).not.toContain("|");
    });

    it("lets the lead of the container delete somebody else's entry but not edit it", async () => {
      const wrapper = await openSomebodyElses({ canDeleteOthers: true });

      // Removing an entry is the lead's right; rewriting one that would still
      // stand under its author's name is nobody's.
      expect(actions(wrapper)).toEqual(["Удалить"]);
    });

    it("offers nothing to a viewer with no account", async () => {
      const wrapper = await openSomebodyElses({ viewerId: null });

      expect(actions(wrapper)).toEqual([]);
    });
  });
});

/**
 * The entry stands beside the list at every width, as on the full version: a
 * phone shows the board of 1366, and no width of the window lays the list over
 * the entry. jsdom lays nothing out, so the stylesheet is read.
 */
describe("NotepadBoard beside its list", () => {
  it("keeps the list and the entry side by side, with no form for a narrow window", () => {
    const css = componentCss(
      join(dirname(fileURLToPath(import.meta.url)), "NotepadBoard.vue"),
    );
    expect(css).not.toMatch(/@media/);
    expect(ruleMap(css).get(".notepad-layout")?.["grid-template-columns"]).toBe(
      "300px 1fr",
    );
  });
});
