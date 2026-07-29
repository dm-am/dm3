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
