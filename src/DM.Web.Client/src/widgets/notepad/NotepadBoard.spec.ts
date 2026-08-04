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
import { describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import NotepadBoard from "./NotepadBoard.vue";
import type { NotepadAdapter } from "./types";
import type { NotepadEntry } from "@/shared/api/models/notepads";

const entry = (over: Partial<NotepadEntry> = {}): NotepadEntry => ({
  id: "1",
  notepadType: "User",
  containerId: "c",
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

const mountBoard = (adapter: NotepadAdapter, props = {}) =>
  mount(NotepadBoard, {
    // Attached: the keyboard tests below assert on document.activeElement,
    // and a detached tree has no focus to move.
    attachTo: document.body,
    global: { plugins: [createPinia()] },
    props: {
      adapter,
      title: "Блокнот",
      emptyText: "Нет записей в блокноте",
      emptyHint: "Создайте первую запись.",
      loadErrorText: "Не удалось загрузить блокнот",
      ...props,
    },
  });

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
    await wrapper.find(".delete-btn").trigger("click");

    // The dialog is teleported to body, so assert on the document.
    expect(document.body.textContent).toContain('Удалить запись "Первая"?');
    expect(adapter.remove).not.toHaveBeenCalled();

    const confirmButton =
      document.body.querySelector<HTMLButtonElement>(".dialog-btn-submit");
    confirmButton?.click();
    await flushPromises();

    expect(adapter.remove).toHaveBeenCalledWith("1");
    expect(wrapper.text()).toContain("Нет записей в блокноте");
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

      expect(wrapper.find(".content-header h2").text()).toBe("Первая");
      expect(wrapper.find(".content-body").text()).toBe("Текст");

      wrapper.unmount();
    });

    it("opens an entry on Space", async () => {
      const wrapper = await mountList();

      await options(wrapper)[1].trigger("keydown", { key: " " });

      expect(wrapper.find(".content-header h2").text()).toBe("Вторая");

      wrapper.unmount();
    });

    it("walks the list with the arrows, Home and End, selection following focus", async () => {
      const wrapper = await mountList();
      const items = options(wrapper);
      const openTitle = () => wrapper.find(".content-header h2").text();

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

  it("reports a failed load instead of showing an empty notepad", async () => {
    const adapter: NotepadAdapter = {
      ...adapterOf([]),
      list: vi.fn().mockResolvedValue({ error: { title: "нет" } }),
    };
    const wrapper = mountBoard(adapter);
    await flushPromises();

    expect(wrapper.find(".notepad-layout").exists()).toBe(false);
  });
});
