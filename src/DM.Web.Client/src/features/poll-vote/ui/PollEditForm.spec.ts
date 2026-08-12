/**
 * @vitest-environment jsdom
 */

/**
 * The polls page renders a card per poll, every card owns an editor of its own,
 * and nothing closes the neighbour when a second one opens. Ids written as
 * constants would then sit on two fields at once, and a `<label for>` — like an
 * `aria-labelledby` — resolves to the first match in the document: a click on
 * the caption of the second editor would land in the first editor's field, and
 * the radio group of one poll would announce the title of another.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import PollEditForm from "./PollEditForm.vue";
import type { Poll } from "@/entities/poll";

vi.mock("@/entities/poll", () => ({
  usePollsStore: () => ({
    editPoll: vi.fn().mockResolvedValue({ data: null, error: null }),
  }),
}));

const poll = (title: string): Poll =>
  ({
    id: "poll-1",
    title,
    details: "",
    startsUtc: "2025-01-01T00:00:00Z",
    endsUtc: "2025-12-31T23:59:59Z",
    isAnonymous: true,
    options: [],
  }) as unknown as Poll;

/** Mounted into the document, so getElementById sees what a browser sees. */
const open = (title: string) => {
  const host = document.createElement("div");
  document.body.appendChild(host);
  return mount(PollEditForm, { props: { poll: poll(title) }, attachTo: host });
};

describe("PollEditForm", () => {
  let editors: ReturnType<typeof open>[] = [];

  beforeEach(() => {
    editors = [open("Первый"), open("Второй")];
  });

  afterEach(() => {
    for (const editor of editors) editor.unmount();
    document.body.innerHTML = "";
  });

  it("gives every field an id of its own while two editors are open", () => {
    const ids = editors.flatMap((editor) =>
      editor.findAll("[id]").map((node) => node.attributes("id")),
    );

    expect(ids).toHaveLength(10);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("points every caption at a control of its own editor", () => {
    for (const editor of editors) {
      const labels = editor.findAll("label.edit-label");
      expect(labels).toHaveLength(4);

      for (const label of labels) {
        const target = document.getElementById(label.attributes("for") ?? "");
        expect(target).not.toBeNull();
        expect(editor.element.contains(target)).toBe(true);
        expect(["INPUT", "TEXTAREA"]).toContain(target?.tagName);
      }
    }
  });

  it("names every radio group by the caption of its own editor", () => {
    for (const editor of editors) {
      const group = editor.get('[role="radiogroup"]');
      const caption = document.getElementById(
        group.attributes("aria-labelledby") ?? "",
      );

      expect(caption).not.toBeNull();
      expect(editor.element.contains(caption)).toBe(true);
      expect(caption?.textContent).toBe("Тип опроса");
    }
  });
});
