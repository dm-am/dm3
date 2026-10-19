/** The data and the mount of the notepad specs; a spec hangs beforeEach(clearToasts). */
import { vi } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia } from "pinia";
import { useToast } from "@/shared/lib/composables/useToast";
import NotepadBoard from "./NotepadBoard.vue";
import type { NotepadAdapter } from "./types";
import type { NotepadEntry } from "@/shared/api/models/notepads";

export const entry = (over: Partial<NotepadEntry> = {}): NotepadEntry => ({
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

export const adapterOf = (entries: NotepadEntry[]): NotepadAdapter => ({
  list: vi.fn().mockResolvedValue({ data: { resources: entries } }),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn().mockResolvedValue({}),
});

export const toast = useToast();

/** What is on screen in toasts right now, in the order it arrived. */
export const said = (): string[] => toast.toasts.value.map((t) => t.message);

/** The toast list is module state shared by every test in the run. */
export function clearToasts() {
  for (const item of [...toast.toasts.value]) toast.dismiss(item.id);
}

export const mountBoard = (adapter: NotepadAdapter, props = {}) =>
  mount(NotepadBoard, {
    // Attached: the keyboard tests read document.activeElement.
    attachTo: document.body,
    global: { plugins: [createPinia()] },
    props: {
      adapter,
      title: "Блокнот",
      // "u" wrote every fixture entry: the default viewer reads their own notes.
      viewerId: "u",
      emptyText: "Нет записей в блокноте",
      emptyHint: "Создайте первую запись.",
      loadErrorText: "Не удалось загрузить блокнот",
      ...props,
    },
  });

/** Opens the editor and writes an entry into it, stopping before the save. */
export async function writeEntry(adapter: NotepadAdapter, content: string) {
  const wrapper = mountBoard(adapter);
  await flushPromises();
  await wrapper.find(".add-btn").trigger("click");
  await wrapper.find("#entry-title").setValue("Свежая");
  await wrapper.find("#entry-content").setValue(content);
  return wrapper;
}

/** Presses the save control: the one button in the footer that is not "Отмена". */
export async function saveEditor(wrapper: VueWrapper) {
  await wrapper
    .find(".editor-actions button:not(.cancel-btn)")
    .trigger("click");
  await flushPromises();
}
