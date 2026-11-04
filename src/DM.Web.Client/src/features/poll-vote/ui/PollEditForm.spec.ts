/**
 * @vitest-environment jsdom
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

vi.mock("@/entities/poll", async (original) => ({
  ...(await original<typeof import("@/entities/poll")>()),
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

// The calendar buttons of the dates stand before "Сохранить".
const saveButton = (editor: ReturnType<typeof open>) =>
  editor.findAll("button").find((button) => button.text() === "Сохранить")!;

// Mounted into the document, so getElementById sees what a browser sees.
const open = (title: string) => {
  const host = document.createElement("div");
  document.body.appendChild(host);
  return mount(PollEditForm, { props: { poll: poll(title) }, attachTo: host });
};

// Two editors of one page, so in one app, as the sidebar's poll cards are.
const openTwo = () => {
  const host = document.createElement("div");
  document.body.appendChild(host);
  return mount(
    {
      components: { PollEditForm },
      template: `<div><PollEditForm :poll="first" /><PollEditForm :poll="second" /></div>`,
      data: () => ({ first: poll("Первый"), second: poll("Второй") }),
    },
    { attachTo: host },
  );
};

describe("PollEditForm", () => {
  let page: ReturnType<typeof openTwo>;
  let editors: ReturnType<typeof page.findComponent>[] = [];

  beforeEach(() => {
    editPoll.mockReset();
    editPoll.mockResolvedValue({ data: null, error: null });
    page = openTwo();
    editors = page.findAllComponents(PollEditForm);
  });

  afterEach(() => {
    page.unmount();
    document.body.innerHTML = "";
  });

  it("gives every field an id of its own while two editors are open", () => {
    const ids = editors.flatMap((editor) =>
      editor.findAll("[id]").map((node) => node.attributes("id")),
    );

    expect(new Set(ids).size).toBe(ids.length);
  });

  it("points every caption at a control of its own editor", () => {
    for (const editor of editors) {
      // Title and details; the dates and the type are groups named below.
      const labels = editor
        .findAll(".form-field-label label")
        .filter((label) => label.attributes("for"));
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
      const rows = editor
        .findAll(".form-field-row")
        .filter((row) => row.find(".date-time-input").exists());
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
      const group = editor
        .findAll('.form-field-row[role="group"]')
        .find((row) => row.find('input[type="radio"]').exists())!;
      const caption = document.getElementById(
        group.attributes("aria-labelledby") ?? "",
      );

      expect(caption).not.toBeNull();
      expect(editor.element.contains(caption)).toBe(true);
      expect(caption?.textContent).toBe("Тип опроса");
    }
  });
});

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

  it.each([0, 401])(
    "leaves a failure of status %i to the interceptor",
    async (status) => {
      editPoll.mockResolvedValue({
        data: null,
        error: { status, title: "Недостаточно прав для этого действия" },
      });

      await save();

      expect(editor.find(".edit-error").exists()).toBe(false);
      // The edit did not land, so the editor stays open.
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

// The editor fills and reads its fields on the site's clock, the one the card prints by.
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

// A begun poll keeps its past start: the bound of creation would make it impossible to edit.
describe("PollEditForm bounds", () => {
  let editor: ReturnType<typeof open>;

  beforeEach(() => {
    editPoll.mockReset();
    editPoll.mockResolvedValue({ data: { id: "poll-1" }, error: null });
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
    expect(editor.find(".form-field-error").exists()).toBe(false);

    await save();

    expect(editPoll).toHaveBeenCalledTimes(1);
    expect(editor.emitted("saved")).toHaveLength(1);
  });

  it("takes no start earlier than the poll's own", async () => {
    const date = editor.findAll(".dti-date .di-input")[0];
    await date.setValue("31.12.2024");
    await date.trigger("blur");

    const line = editor.get(".form-field-error");
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

    const line = editor.findAll(".form-field-error");
    expect(line).toHaveLength(1);
    expect(line[0].text()).toMatch(/^Не раньше /);
    const group = editor
      .findAll(".form-field-row")
      .filter((row) => row.find(".date-time-input").exists())[1];
    expect(group.attributes("aria-describedby")).toBe(line[0].attributes("id"));
    await save();
    expect(editPoll).not.toHaveBeenCalled();
  });
});
