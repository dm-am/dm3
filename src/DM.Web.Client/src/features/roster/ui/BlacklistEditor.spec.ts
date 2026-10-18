/**
 * @vitest-environment jsdom
 */

/**
 * The blacklist of the site, as the game, the blog and the account show it: a
 * table whose rows are added and removed in place, drawn after the editor of
 * a character sheet (AttributeSchemaEditor). The site's table "Пользователь",
 * the bin at the end of each row, and the field and "Добавить" of
 * BlacklistAddRow as the last line of the table, drawn right under the box the
 * table scrolls in so that the suggestions of the field open past it. The game and the blog say over
 * it in a grey line what the list does; the account's rows carry the day the
 * name went on. The sections have their own specs for their wiring; this one
 * holds what they share.
 *
 * What these catch:
 *  - the line of what the list does put back into body text, or dropped;
 *  - the names drawn as a column of words instead of the table, or the day
 *    lost from the account;
 *  - the field moved away from under the table, or into the box the table
 *    scrolls in, where the suggestions of the field are clipped; a
 *    "+ Добавить" or a window back;
 *  - the bin drawn as a word, asking first, sending twice, or saying its
 *    refusal anywhere but under its own row;
 *  - "В черном списке пока никого нет" said while the list is on its way, or
 *    said over a list that did not come (it is not empty, it is unknown);
 *  - the field suggesting the viewer, the names on the list, or the people
 *    the section leaves out;
 *  - a name cut with "..." or put under a tooltip.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { readFileSync, readdirSync, statSync } from "fs";
import { join, resolve } from "path";
import type { User } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import type { BlacklistRow } from "../model/types";
import BlacklistAddRow from "./BlacklistAddRow.vue";
import BlacklistEditor from "./BlacklistEditor.vue";

const HINT = "Внесенные не пишут комментарии в блоге.";

const aRow = (username: string, createdUtc?: string): BlacklistRow => ({
  id: `id-${username}`,
  username,
  user: { id: `id-${username}`, username } as unknown as User,
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

/**
 * The last line of the table, where a name goes on: right after the box the
 * table scrolls in and not inside it. A box that scrolls sideways clips what
 * stands past its bottom too, and the suggestions of the field open down.
 */
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

  it("говорит серой строкой над таблицей, что делает список", () => {
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

  it('ставит имена таблицей "Пользователь", у каждой строки корзина', () => {
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
      // A sign, not a word.
      expect(bin.text()).toBe("");
    }
    // No date in the game and the blog: the server does not hand it.
    expect(w.text()).not.toMatch(/\d\d\.\d\d\.\d{4}/);
  });

  it('ставит день внесения столбцом "Внесен", где он есть', () => {
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

  it("ставит поле и Добавить последней строкой таблицы", () => {
    const w = mountEditor({ rows: [aRow("Ланселот")] });

    expect(addLine(w).findComponent(BlacklistAddRow).exists()).toBe(true);
    expect(w.findAll("button").map((b) => b.text())).not.toContain(
      "+ Добавить",
    );
  });

  it('пустой список говорит "В черном списке пока никого нет" в таблице', () => {
    const w = mountEditor();

    expect(w.get("tbody").text()).toBe("В черном списке пока никого нет");
    expect(bodyRows(w)).toHaveLength(0);
    expect(addLine(w).findComponent(BlacklistAddRow).exists()).toBe(true);
  });

  it("не называет список пустым, пока он грузится", () => {
    const w = mountEditor({ loading: true });

    expect(w.text()).not.toContain("пока никого нет");
    expect(addLine(w).findComponent(BlacklistAddRow).exists()).toBe(true);
  });

  it("не выдает неудачную загрузку за пустой список", () => {
    const w = mountEditor({ error: "Не удалось загрузить черный список" });

    expect(w.get("tbody .action-result").text()).toBe(
      "Не удалось загрузить черный список",
    );
    expect(w.text()).not.toContain("пока никого нет");
    // A good add reads the list again, so the field stays.
    expect(addLine(w).findComponent(BlacklistAddRow).exists()).toBe(true);
  });

  it("удаляет имя корзиной, ничего не спрашивая и один раз", async () => {
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

    // Shut while its answer is on the wire; the other row stays live.
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

  it("пишет отказ под той строкой, которую не удалось удалить", async () => {
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

  it("не дает полю подсказывать себя, внесенных и исключенных, и отдает ему add", async () => {
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

  it("не режет имя и не прячет его в подсказку", () => {
    const source = readFileSync(
      resolve(__dirname, "BlacklistEditor.vue"),
      "utf8",
    );

    expect(source).not.toMatch(/text-overflow/);
    expect(source).not.toMatch(/Tooltip/);
    expect(source).not.toMatch(/overflow:\s*hidden/);
  });

  // The name is put on at the place, in all three lists.
  it("нигде в черных списках не открывает окна", () => {
    const roster = resolve(__dirname, "..");
    const files = (dir: string): string[] =>
      readdirSync(dir).flatMap((name) => {
        const full = join(dir, name);
        return statSync(full).isDirectory() ? files(full) : [full];
      });
    const sources = [
      ...files(roster).filter(
        (f) => /\.(vue|ts)$/.test(f) && !/\.spec\.ts$/.test(f),
      ),
      resolve(
        __dirname,
        "../../../pages/account/sections/AccountBlacklistSection.vue",
      ),
    ];

    for (const file of sources) {
      const text = readFileSync(file, "utf8");
      expect(text, file).not.toMatch(/useModal|BlockUserDialog|block-user/);
      expect(text, file).not.toMatch(/import\s+Dialog\b|Layout\/Dialog\.vue/);
    }
  });
});
