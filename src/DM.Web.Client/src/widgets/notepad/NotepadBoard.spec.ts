/**
 * @vitest-environment jsdom
 */

/** The board the three notepads share: its gate, its list, its delete. */
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
  toast,
  writeEntry,
} from "./NotepadBoard.fixtures";

beforeEach(clearToasts);

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
    // The gate may flip after mount, while the container store loads.
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

    // The question prints a refusal itself, so the request keeps the interceptor quiet.
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

  // The reader pane opens only from a selected entry: the list is a listbox.
  describe("entry list is an operable listbox", () => {
    const threeEntries = () => [
      entry({ id: "1", title: "Первая", sortOrder: 1 }),
      entry({ id: "2", title: "Вторая", sortOrder: 2 }),
      entry({ id: "3", title: "Третья", sortOrder: 3 }),
    ];

    const mountList = async () => {
      const wrapper = mountBoard(adapterOf(threeEntries()));
      await flushPromises();
      // A precondition: the tests below drive the list through its options.
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

      // Nothing selected: the tab stop sits on the first entry.
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

  /** A created entry is what the pane shows. */
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

    const write = (adapter: NotepadAdapter) =>
      writeEntry(adapter, "Только что написано");

    it("opens the created entry, where a cancel leaves the placeholder", async () => {
      const adapter = adapterCreating(made());
      const wrapper = await write(adapter);

      // The other ending, for contrast: nothing written, the placeholder.
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

  // One main button to a form.
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
    // In the place of the list, in the server's words, with the way to ask again.
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

  /** The controls over an entry repeat the server's rules: the author edits, the author or lead deletes. */
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

      // Removing is the lead's right; rewriting under the author's name is nobody's.
      expect(actions(wrapper)).toEqual(["Удалить"]);
    });

    it("offers nothing to a viewer with no account", async () => {
      const wrapper = await openSomebodyElses({ viewerId: null });

      expect(actions(wrapper)).toEqual([]);
    });
  });
});
