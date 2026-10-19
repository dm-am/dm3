/**
 * @vitest-environment jsdom
 */

/**
 * A moderator's note signs itself in one caption line, as it reads and copies:
 * the date, the edit mark run into it, then the actions after a bar, gone
 * while the note is in the editor.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import type { ModNote } from "@/shared/api/models/moderation";
import ModerationNotes from "./ModerationNotes.vue";

const createModNote = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({ moderationApi: { createModNote } }));

// The editor is stubbed; the line above it is what is read.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: { template: "<div class='editor-stub' />" },
}));

const note = (over: Partial<ModNote> = {}): ModNote => ({
  id: "n-1",
  authorUsername: "Модератор",
  authorId: "u-1",
  text: "Предупрежден устно",
  createdUtc: "2026-05-01T12:00:00",
  canEdit: true,
  canDelete: true,
  ...over,
});

const mountNotes = (notes: ModNote[]) =>
  mount(ModerationNotes, {
    props: { notes, canCreate: false, targetUsername: "Нарушитель" },
  });

/** The header of the first note as it reads, spaces collapsed. */
const header = (wrapper: ReturnType<typeof mountNotes>) =>
  wrapper.get(".mod-note_header").text().replace(/\s+/g, " ");

describe("ModerationNotes", () => {
  it("signs an edited note in one line with its actions after the mark of the edit", () => {
    const wrapper = mountNotes([note({ modifiedUtc: "2026-05-02T09:00:00" })]);

    expect(header(wrapper)).toMatch(
      /^Модератор 01\.05\.2026 \S.* \(изменено 02\.05\.2026 \S.*\) \| Редактировать \| Удалить$/,
    );
    expect(wrapper.find(".mod-note_text").text()).toBe("Предупрежден устно");
  });

  it("puts the bar straight after the date of a note never edited", () => {
    expect(header(mountNotes([note()]))).toMatch(
      /^Модератор 01\.05\.2026 \S.* \| Редактировать \| Удалить$/,
    );
  });

  it("ends on the date for a reader who may not touch the note", () => {
    const wrapper = mountNotes([note({ canEdit: false, canDelete: false })]);

    expect(header(wrapper)).toMatch(/^Модератор 01\.05\.2026 [^|]*$/);
    expect(wrapper.find("button").exists()).toBe(false);
  });

  it("drops the actions while the note is open in the editor", async () => {
    const wrapper = mountNotes([note()]);

    await wrapper.get(".caption-action").trigger("click");

    expect(wrapper.find(".editor-stub").exists()).toBe(true);
    expect(header(wrapper)).not.toContain("|");
    expect(wrapper.find(".mod-note_text").exists()).toBe(false);
  });
});

/** Notes add the way the viewer's own note does: a grey "Добавить" opens the editor. */
describe("adding a moderator's note", () => {
  beforeEach(() => createModNote.mockReset());

  const mountAdding = (notes: ModNote[] = []) =>
    mount(ModerationNotes, {
      props: { notes, canCreate: true, targetUsername: "Нарушитель" },
    });

  const buttons = (wrapper: ReturnType<typeof mountAdding>) =>
    wrapper.findAll("button").map((b) => b.text());

  it("keeps the editor shut until Добавить is pressed", async () => {
    const wrapper = mountAdding();

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.get(".mod-note_empty").text()).toBe("Заметок нет");
    expect(buttons(wrapper)).toEqual(["Добавить"]);

    await wrapper.get(".mod-note_add .caption-action").trigger("click");

    expect(wrapper.find(".editor-stub").exists()).toBe(true);
    expect(buttons(wrapper)).toEqual(["Сохранить", "Отмена"]);
  });

  it("shuts the editor again on Отмена, with nothing sent", async () => {
    const wrapper = mountAdding();
    await wrapper.get(".mod-note_add .caption-action").trigger("click");

    const cancel = wrapper
      .findAll("button")
      .find((b) => b.text() === "Отмена")!;
    await cancel.trigger("click");

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(createModNote).not.toHaveBeenCalled();
  });

  it("sends the note, shuts the editor and has the profile read again", async () => {
    createModNote.mockResolvedValue({ data: {}, error: null });
    const wrapper = mountAdding([note()]);
    await wrapper.get(".mod-note_add .caption-action").trigger("click");

    wrapper
      .findComponent(BBCodeEditor)
      .vm.$emit("update:modelValue", " Обещал исправиться ");
    await wrapper.vm.$nextTick();
    const save = wrapper
      .findAll("button")
      .find((b) => b.text() === "Сохранить")!;
    await save.trigger("click");
    await flushPromises();

    expect(createModNote).toHaveBeenCalledWith(
      "Нарушитель",
      " Обещал исправиться ",
    );
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.emitted("updated")).toHaveLength(1);
  });
});
