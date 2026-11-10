/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import type { GeneralError } from "@/shared/api/models/common";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import type { BlacklistRow } from "../model/types";
import BlacklistAddRow from "./BlacklistAddRow.vue";
import BlacklistEditor from "./BlacklistEditor.vue";

const HINT = "Внесенные не пишут комментарии в блоге.";

const aRow = (username: string, createdUtc?: string): BlacklistRow => ({
  id: `id-${username}`,
  username,
  user: createMockUser(`id-${username}`, username),
  createdUtc,
});

function mountEditor(
  over: Partial<{
    hint: string;
    rows: BlacklistRow[];
    loading: boolean;
    dated: boolean;
    error: string | null;
    add: (username: string) => Promise<GeneralError | null>;
    remove: (username: string) => Promise<GeneralError | null>;
    exclude: string[];
  }> = {},
) {
  return mount(BlacklistEditor, {
    props: {
      hint: HINT,
      rows: [],
      loading: false,
      add: vi.fn().mockResolvedValue(null),
      remove: vi.fn().mockResolvedValue(null),
      ...over,
    },
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
}

type Editor = ReturnType<typeof mountEditor>;

const bodyRows = (w: Editor) => w.findAll("tbody tr.table-row");
const names = (w: Editor) =>
  bodyRows(w).map((row) => row.get(".col-user").text());
const binOf = (w: Editor, index: number) =>
  bodyRows(w)[index].get(".col-action button");
const headers = (w: Editor) =>
  w.findAll("thead th").map((th) => th.text().trim());

// Right after the box the table scrolls in: inside it, the suggestions would be clipped.
function addLine(w: Editor) {
  const box = w.get(".data-table-scroll").element;
  const line = w.get(".bl-foot");
  expect(box.contains(line.element)).toBe(false);
  expect(box.nextElementSibling).toBe(line.element);
  return line;
}

describe("BlacklistEditor", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("user", JSON.stringify({ username: "Артур" }));
    setActivePinia(createPinia());
  });

  afterEach(() => vi.restoreAllMocks());

  it("says in a grey line over the table what the list does", () => {
    const w = mountEditor();

    const line = w.get(".bl-hint");
    expect(line.text()).toBe(HINT);
    expect(line.classes()).toContain("secondary-text");
    expect(
      line.element.compareDocumentPosition(w.get("table").element) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(mountEditor({ hint: undefined }).find(".bl-hint").exists()).toBe(
      false,
    );
  });

  it('lays the names out as a "Пользователь" table with a bin on each row', () => {
    const w = mountEditor({
      rows: [aRow("Ланселот"), aRow("Мордред")],
    });

    expect(headers(w)).toEqual(["Пользователь", ""]);
    expect(names(w)).toEqual(["Ланселот", "Мордред"]);
    for (const row of bodyRows(w)) {
      const bin = row.getComponent(RowIconButton);
      expect(bin.props("icon")).toBe("trash");
      expect(bin.attributes("aria-label")).toMatch(
        /^Удалить .+ из черного списка$/,
      );
      expect(bin.text()).toBe("");
    }
    // No date in the game and the blog: the server does not hand it.
    expect(w.text()).not.toMatch(/\d\d\.\d\d\.\d{4}/);
  });

  it('puts the day a name went on in a "Внесен" column where there is one', () => {
    const w = mountEditor({
      dated: true,
      hint: undefined,
      rows: [aRow("Ланселот", "2026-09-21T10:00:00Z")],
    });

    expect(headers(w)).toEqual(["Пользователь", "Внесен", ""]);
    const cells = bodyRows(w)[0].findAll("td");
    expect(cells.map((cell) => cell.text())).toEqual([
      "Ланселот",
      "21.09.2026",
      "",
    ]);
  });

  it("puts the field and Добавить in the last line of the table", () => {
    const w = mountEditor({ rows: [aRow("Ланселот")] });

    expect(addLine(w).findComponent(BlacklistAddRow).exists()).toBe(true);
    expect(w.findAll("button").map((b) => b.text())).not.toContain(
      "+ Добавить",
    );
  });

  it('says "В черном списке пока никого нет" in the table of an empty list', () => {
    const w = mountEditor();

    expect(w.get("tbody").text()).toBe("В черном списке пока никого нет");
    expect(bodyRows(w)).toHaveLength(0);
    expect(addLine(w).findComponent(BlacklistAddRow).exists()).toBe(true);
  });

  it("does not call the list empty while it loads", () => {
    const w = mountEditor({ loading: true });

    expect(w.text()).not.toContain("пока никого нет");
    expect(addLine(w).findComponent(BlacklistAddRow).exists()).toBe(true);
  });

  it("does not pass a failed load off as an empty list", () => {
    const w = mountEditor({ error: "Не удалось загрузить черный список" });

    expect(w.get("tbody .action-result").text()).toBe(
      "Не удалось загрузить черный список",
    );
    expect(w.text()).not.toContain("пока никого нет");
    expect(addLine(w).findComponent(BlacklistAddRow).exists()).toBe(true);
  });

  it("removes a name with the bin, once and without asking", async () => {
    const answers: Array<(value: null) => void> = [];
    const remove = vi.fn(
      () =>
        new Promise<GeneralError | null>((resolve) => {
          answers.push(resolve);
        }),
    );
    const w = mountEditor({
      rows: [aRow("Мордред"), aRow("Моргана")],
      remove,
    });

    await binOf(w, 0).trigger("click");

    expect(binOf(w, 0).attributes("disabled")).toBeDefined();
    expect(binOf(w, 0).attributes("aria-busy")).toBe("true");
    expect(binOf(w, 1).attributes("disabled")).toBeUndefined();
    await binOf(w, 0).trigger("click");
    await binOf(w, 1).trigger("click");
    answers.forEach((answer) => answer(null));
    await flushPromises();

    expect(remove.mock.calls).toEqual([["Мордред"], ["Моргана"]]);
    expect(binOf(w, 0).attributes("disabled")).toBeUndefined();
    expect(document.querySelector('[role="dialog"]')).toBeNull();
  });

  it("says the refusal under the row that could not be removed", async () => {
    const remove = vi.fn().mockResolvedValue({
      type: "",
      title: "Пользователя нет в черном списке",
      status: 409,
      traceId: "t",
    });
    const w = mountEditor({
      rows: [aRow("Ланселот"), aRow("Мордред"), aRow("Персиваль")],
      remove,
    });

    await binOf(w, 1).trigger("click");
    await flushPromises();

    const details = w.findAll("tbody tr.table-details-row");
    expect(details).toHaveLength(1);
    expect(details[0].text()).toBe("Пользователя нет в черном списке");
    expect(details[0].element.previousElementSibling).toBe(
      bodyRows(w)[1].element,
    );
    expect(names(w)).toEqual(["Ланселот", "Мордред", "Персиваль"]);
  });

  it("keeps the viewer, the listed and the excluded out of the suggestions and hands the field add", async () => {
    const add = vi.fn().mockResolvedValue(null);
    const w = mountEditor({
      rows: [aRow("Мордред")],
      exclude: ["Мерлин"],
      add,
    });

    const row = w.getComponent(BlacklistAddRow);
    expect([...(row.props("exclude") as string[])].sort()).toEqual(
      ["Артур", "Мерлин", "Мордред"].sort(),
    );
    await (row.props("add") as (name: string) => Promise<unknown>)("Моргана");
    expect(add).toHaveBeenCalledWith("Моргана");
  });
});
