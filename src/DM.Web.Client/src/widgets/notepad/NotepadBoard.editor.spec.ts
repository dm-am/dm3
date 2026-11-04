/**
 * @vitest-environment jsdom
 */

/** The editor of one entry, driven through the board: opening, saving, refusals, the cross. */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import type { NotepadAdapter } from "./types";
import type { NotepadEntry } from "@/shared/api/models/notepads";
import {
  adapterOf,
  clearToasts,
  entry,
  mountBoard,
  said,
  saveEditor as save,
  writeEntry,
} from "./NotepadBoard.fixtures";

beforeEach(clearToasts);

describe("NotepadBoard", () => {
  /** A refused save is answered inside the editor, for the statuses the interceptor leaves. */
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

    const write = (adapter: NotepadAdapter) => writeEntry(adapter, "Написано");

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
      // A 2xx with nothing in it: no status to consult and no entry to open.
      const wrapper = await write(adapterAnswering({ data: {}, error: null }));
      await save(wrapper);

      expect(line(wrapper).text()).toBe("Не удалось сохранить запись");
      expect(wrapper.find(".editor").exists()).toBe(true);
      expect(said()).toEqual([]);

      wrapper.unmount();
    });

    /** The editor's line takes the server's words; 401 and a dead connection keep the plate. */
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
        // Silent, not closed: the editor keeps what was typed.
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

      // The second attempt went through: the pane shows what was saved.
      expect(wrapper.find(".editor").exists()).toBe(false);
      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(wrapper.find(".content-title").text()).toBe("Свежая");

      wrapper.unmount();
    });

    it("starts over when a new entry is asked for with the editor still open", async () => {
      const wrapper = await write(adapterAnswering(refusal(404)));
      await save(wrapper);
      expect(line(wrapper).exists()).toBe(true);

      // No "Отмена": the editor still stands when a fresh one is asked for.
      await wrapper.find(".add-btn").trigger("click");

      expect(line(wrapper).exists()).toBe(false);
      expect(typedTitle(wrapper)).toBe("");
      expect(
        (wrapper.find("#entry-content").element as HTMLTextAreaElement).value,
      ).toBe("");

      wrapper.unmount();
    });
  });

  /** An untitled entry is refused under the field, as FormField prints it. */
  describe("an untitled entry is refused under the field", () => {
    const openEditor = async () => {
      const adapter = adapterOf([entry()]);
      const wrapper = mountBoard(adapter);
      await flushPromises();
      await wrapper.find(".add-btn").trigger("click");
      return { adapter, wrapper };
    };

    it("prints the reason under the field and nothing in the corner", async () => {
      const { adapter, wrapper } = await openEditor();

      await save(wrapper);

      const line = wrapper.find(".form-field-error");
      expect(line.exists()).toBe(true);
      expect(line.text()).toBe("Введите название записи");
      expect(said()).toEqual([]);
      // Nothing asked of the server; the editor still stands.
      expect(adapter.create).not.toHaveBeenCalled();
      expect(wrapper.find(".editor").exists()).toBe(true);

      wrapper.unmount();
    });

    it("points the field at the line, so the two are read together", async () => {
      const { wrapper } = await openEditor();
      await save(wrapper);

      const field = wrapper.find("#entry-title");
      expect(field.attributes("aria-invalid")).toBe("true");
      expect(field.attributes("aria-describedby")).toBe(
        wrapper.find(".form-field-error").attributes("id"),
      );
      // The frame turns with the line.
      expect(wrapper.find(".form-field").classes()).toContain("error");

      wrapper.unmount();
    });

    it("takes the line off on the first character typed", async () => {
      const { wrapper } = await openEditor();
      await save(wrapper);
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
      await save(wrapper);

      await wrapper.find(".cancel-btn").trigger("click");
      await wrapper.find(".add-btn").trigger("click");

      expect(wrapper.find(".form-field-error").exists()).toBe(false);

      wrapper.unmount();
    });
  });

  /** The cross closes the editor mid-save; the server keeps the entry, so the list shows it. */
  it("keeps an entry saved behind a closed editor in the list", async () => {
    let answer!: (value: object) => void;
    const create = vi
      .fn()
      .mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    const wrapper = mountBoard({ ...adapterOf([entry()]), create });
    await flushPromises();
    await wrapper.find(".add-btn").trigger("click");
    await wrapper.find("#entry-title").setValue("Свежая");
    await save(wrapper);

    // The save is still on the wire when the cross closes the editor.
    await wrapper.find(".close-btn").trigger("click");
    expect(wrapper.find(".editor").exists()).toBe(false);

    answer({
      data: { resource: entry({ id: "2", title: "Свежая", sortOrder: 2 }) },
      error: null,
    });
    await flushPromises();

    expect(wrapper.findAll(".entry-title").map((n) => n.text())).toEqual([
      "Первая",
      "Свежая",
    ]);
    expect(wrapper.find(".editor").exists()).toBe(false);

    wrapper.unmount();
  });

  // The same race over an edit: the saved entry takes the place of the one it rewrote.
  it("keeps an entry edited behind a closed editor in its place", async () => {
    let answer!: (value: object) => void;
    const update = vi
      .fn()
      .mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    const wrapper = mountBoard({ ...adapterOf([entry()]), update });
    await flushPromises();
    await wrapper.find(".entry-item").trigger("click");
    const edit = wrapper
      .findAll(".content-meta .caption-action")
      .find((b) => b.text() === "Редактировать")!;
    await edit.trigger("click");
    await wrapper.find("#entry-title").setValue("Правка");
    await save(wrapper);

    // The save is still on the wire when the cross closes the editor.
    await wrapper.find(".close-btn").trigger("click");
    expect(wrapper.find(".editor").exists()).toBe(false);

    answer({
      data: { resource: entry({ id: "1", title: "Правка" }) },
      error: null,
    });
    await flushPromises();

    expect(wrapper.findAll(".entry-title").map((n) => n.text())).toEqual([
      "Правка",
    ]);

    wrapper.unmount();
  });
});
