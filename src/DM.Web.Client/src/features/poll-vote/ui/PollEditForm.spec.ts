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

// The device is in Moscow for the file (see "PollEditForm dates" below).
const MACHINE_ZONE = process.env.TZ;
process.env.TZ = "Europe/Moscow";

import {
  describe,
  it,
  expect,
  vi,
  beforeEach,
  afterEach,
  afterAll,
} from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import PollEditForm from "./PollEditForm.vue";
import { setAccountTimeZone } from "@/shared/lib/utils/timeZone";
import type { Poll } from "@/entities/poll";

const { editPoll } = vi.hoisted(() => ({ editPoll: vi.fn() }));

vi.mock("@/entities/poll", () => ({
  usePollsStore: () => ({ editPoll }),
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

/** "Сохранить": the calendar buttons of the dates stand before it. */
const saveButton = (editor: ReturnType<typeof open>) =>
  editor.findAll("button").find((button) => button.text() === "Сохранить")!;

/** Mounted into the document, so getElementById sees what a browser sees. */
const open = (title: string) => {
  const host = document.createElement("div");
  document.body.appendChild(host);
  return mount(PollEditForm, { props: { poll: poll(title) }, attachTo: host });
};

describe("PollEditForm", () => {
  let editors: ReturnType<typeof open>[] = [];

  beforeEach(() => {
    editPoll.mockReset();
    editPoll.mockResolvedValue({ data: null, error: null });
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
      expect(labels).toHaveLength(2);

      for (const label of labels) {
        const target = document.getElementById(label.attributes("for") ?? "");
        expect(target).not.toBeNull();
        expect(editor.element.contains(target)).toBe(true);
        expect(["INPUT", "TEXTAREA"]).toContain(target?.tagName);
      }
    }
  });

  it("names each row of a date and a time by the caption of its own editor", () => {
    for (const editor of editors) {
      const rows = editor.findAll(".date-time-input");
      expect(rows).toHaveLength(2);

      expect(
        rows.map((row) => {
          expect(row.attributes("role")).toBe("group");
          const caption = document.getElementById(
            row.attributes("aria-labelledby") ?? "",
          );
          expect(editor.element.contains(caption)).toBe(true);
          return caption?.textContent;
        }),
      ).toEqual(["Начало", "Окончание"]);
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

/**
 * One refusal, one carrier.
 *
 * The editor has a line beside its save button and the response interceptor has
 * a plate. For a refused edit, a rate limit, a dead server, a dead connection
 * or an expired session the plate is the carrier and the line keeps quiet — the
 * two together were one press refused twice on one card. What is left for the
 * line is what the server said about the values that were sent.
 */
describe("PollEditForm refusals", () => {
  let editor: ReturnType<typeof open>;

  beforeEach(() => {
    editPoll.mockReset();
    editor = open("Первый");
  });

  afterEach(() => {
    editor.unmount();
    document.body.innerHTML = "";
  });

  const save = async () => {
    await saveButton(editor).trigger("click");
    await flushPromises();
  };

  // The refusals the interceptor used to plate stand beside the button now,
  // with the plate taken down (refusalHere; the owner's rule of 26 September
  // 2026). The editor stays open with what was typed.
  it.each([
    [403, "Недостаточно прав для этого действия"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Недостаточно прав для этого действия"],
    [503, "Недостаточно прав для этого действия"],
  ])("says a failure of status %i beside the button", async (status, line) => {
    editPoll.mockResolvedValue({
      data: null,
      error: { status, title: "Недостаточно прав для этого действия" },
    });

    await save();

    expect(editor.get(".edit-error").text()).toBe(line);
    expect(editor.emitted("saved")).toBeUndefined();
  });

  // An expired session and a dead connection keep the plate.
  it.each([0, 401])(
    "leaves a failure of status %i to the interceptor",
    async (status) => {
      editPoll.mockResolvedValue({
        data: null,
        error: { status, title: "Недостаточно прав для этого действия" },
      });

      await save();

      expect(editor.find(".edit-error").exists()).toBe(false);
      // Silent about the failure, not about the edit: it did not land, so the
      // editor stays open instead of reporting itself saved.
      expect(editor.emitted("saved")).toBeUndefined();
    },
  );

  it("prints what the server said about the values", async () => {
    editPoll.mockResolvedValue({
      data: null,
      error: { status: 409, title: "Опрос уже начался" },
    });

    await save();

    expect(editor.get(".edit-error").text()).toBe("Опрос уже начался");
  });

  it("reports the edit when it landed", async () => {
    editPoll.mockResolvedValue({ data: { id: "poll-1" }, error: null });

    await save();

    expect(editor.find(".edit-error").exists()).toBe(false);
    expect(editor.emitted("saved")).toHaveLength(1);
  });
});

/**
 * The card of a poll prints its dates on the site's clock, the account's zone
 * for a signed-in reader. The editor opened from that card used to fill its
 * fields on the device's clock and read them back the same way: a moderator in
 * Tokyo on a device in Moscow saw 20:00 on the card, 14:00 in the editor, and a
 * 20:00 typed there was sent as 20:00 Moscow.
 */
describe("PollEditForm dates", () => {
  let editor: ReturnType<typeof open>;

  beforeEach(() => {
    editPoll.mockReset();
    editPoll.mockResolvedValue({ data: { id: "poll-1" }, error: null });
    setAccountTimeZone("Asia/Tokyo");
    editor = open("Первый");
  });

  afterEach(() => {
    editor.unmount();
    document.body.innerHTML = "";
    setAccountTimeZone(null);
  });

  afterAll(() => {
    if (MACHINE_ZONE === undefined) delete process.env.TZ;
    else process.env.TZ = MACHINE_ZONE;
  });

  /** The date and the time of a row, as they read in the fields. */
  const row = (index: number) => {
    const dates = editor.findAll(".dti-date .di-input");
    const times = editor.findAll("input.dti-time");
    return { date: dates[index], time: times[index] };
  };
  const shown = (index: number) =>
    [row(index).date, row(index).time]
      .map((one) => (one.element as HTMLInputElement).value)
      .join(" ");

  it("opens on the account's clock", () => {
    expect(shown(0)).toBe("01.01.2025 09:00");
    expect(shown(1)).toBe("01.01.2026 08:59");
  });

  it("sends 20:00 typed in Tokyo as 20:00 in Tokyo", async () => {
    await row(0).time.setValue("20:00");
    await saveButton(editor).trigger("click");
    await flushPromises();

    expect(editPoll).toHaveBeenCalledWith(
      "poll-1",
      expect.objectContaining({
        startsUtc: "2025-01-01T11:00:00.000Z",
        endsUtc: "2025-12-31T23:59:00.000Z",
      }),
    );
  });
});

/**
 * The bounds of an edit (M9 of the critique, 5 October 2026). A poll that has
 * begun has its start in the past; the bound of creation ("no earlier than
 * now") on its editor would have made that start a mistake and the poll
 * impossible to edit. The start goes no earlier than the earlier of its own
 * and now, and the end comes after the start.
 */
describe("PollEditForm bounds", () => {
  let editor: ReturnType<typeof open>;

  beforeEach(() => {
    editPoll.mockReset();
    editPoll.mockResolvedValue({ data: { id: "poll-1" }, error: null });
    // The poll of the file began on 1 January 2025 and it is later than that.
    editor = open("Первый");
  });

  afterEach(() => {
    editor.unmount();
    document.body.innerHTML = "";
  });

  const save = async () => {
    await saveButton(editor).trigger("click");
    await flushPromises();
  };

  it("saves a poll that has begun, its start in the past", async () => {
    expect(editor.find(".edit-field-error").exists()).toBe(false);

    await save();

    expect(editPoll).toHaveBeenCalledTimes(1);
    expect(editor.emitted("saved")).toHaveLength(1);
  });

  it("takes no start earlier than the poll's own", async () => {
    const date = editor.findAll(".dti-date .di-input")[0];
    await date.setValue("31.12.2024");
    await date.trigger("blur");

    const line = editor.get(".edit-field-error");
    expect(line.text()).toMatch(/^Не раньше 01.01.2025/);
    await save();
    expect(editPoll).not.toHaveBeenCalled();
    expect(editor.get(".edit-error").text()).toBe(
      "Проверьте начало и окончание опроса",
    );
  });

  it("says the end may not come before the start, and does not send", async () => {
    const date = editor.findAll(".dti-date .di-input")[1];
    await date.setValue("01.06.2024");
    await date.trigger("blur");

    const line = editor.findAll(".edit-field-error");
    expect(line).toHaveLength(1);
    expect(line[0].text()).toMatch(/^Не раньше /);
    // The row is described by the line under it.
    const group = editor.findAll(".date-time-input")[1];
    expect(group.attributes("aria-describedby")).toBe(line[0].attributes("id"));
    await save();
    expect(editPoll).not.toHaveBeenCalled();
  });
});
