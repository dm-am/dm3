/**
 * @vitest-environment jsdom
 */

/**
 * The account's own blacklist: under the caption "В черном списке" the table
 * the game and the blog draw (BlacklistEditor), "Пользователь", "Внесен" in a
 * column of its own and the bin at the end of a row, with the field that puts
 * a name on as the last line of the table (owner, 13 October 2026, of the
 * three blacklists: "добавить сделай нижней строкой"). The server hands the
 * list back newest first, so a name put on is the first row. Taking a name off
 * asks nothing.
 *
 * What these catch:
 *  - the day back in the cell of the name, or the headers in other words;
 *  - a table of its own instead of the one of the game and the blog;
 *  - a window back in front of the field, or the field out of the table;
 *  - unshift turned into push: the new row at the far end;
 *  - a list that did not come said to be empty;
 *  - "Показать еще" missing on a list longer than a page, shown on one that
 *    is all there, or asking for a page the rows on screen do not end at;
 *  - the toast put back: one event announced twice;
 *  - a setting that reads only under a heading ("Скрывать игры" - whose?);
 *  - the box of a setting that wraps hung between its two lines.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { readFileSync } from "fs";
import { resolve } from "path";
import { blacklistApi, UserAutocomplete } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { BlacklistEntry } from "@/shared/api/models/personal";
import type { Column } from "@/shared/ui/DataTable";
import { BlacklistEditor } from "@/features/roster";
import AccountBlacklistSection from "./AccountBlacklistSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const anEntry = (username: string, createdUtc: string): BlacklistEntry => ({
  id: `entry-${username}`,
  username,
  createdUtc,
});

/** A page of the list as the server answers it, with the count of all. */
const aPage = (entries: BlacklistEntry[], total = entries.length) =>
  ({
    data: {
      resources: entries,
      paging: { pages: 1, current: 1, skip: 0, take: 50, total },
    },
    error: null,
  }) as never;

const refused = (status: number, title = "") =>
  ({ data: null, error: { type: "", title, status, traceId: "t" } }) as never;

/** The section with a blacklist already on screen. */
async function mountSection(entries: BlacklistEntry[], total?: number) {
  setActivePinia(createPinia());

  vi.spyOn(blacklistApi, "getSettings").mockResolvedValue({
    data: {
      hideComments: false,
      hideMessages: false,
      hideGames: false,
      hideBlogs: false,
      blockDirectMessages: false,
      autoPopulateContentBlacklist: false,
    },
    error: null,
  } as never);
  const getBlacklist = vi
    .spyOn(blacklistApi, "getBlacklist")
    .mockResolvedValue(aPage(entries, total));

  const wrapper = mount(AccountBlacklistSection, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  await flushPromises();
  return { wrapper, getBlacklist };
}

type Section = Awaited<ReturnType<typeof mountSection>>["wrapper"];

/** The blocked names, in the order the reader reads them. */
const namesOnScreen = (wrapper: Section) =>
  wrapper.findAll(".table-row .user-link").map((link) => link.text());

/** The bin of every row, top to bottom. */
const removeLinks = (wrapper: Section) =>
  wrapper.findAll(".table-row .col-action button");

const button = (wrapper: Section, word: string) =>
  wrapper.findAll("button").find((b) => b.text() === word);

const field = (wrapper: Section) => wrapper.getComponent(UserAutocomplete);

/** Picks a name in the field and presses "Добавить". */
async function addByField(wrapper: Section, entry: BlacklistEntry) {
  vi.spyOn(blacklistApi, "blockUser").mockResolvedValue({
    data: entry,
    error: null,
  } as never);
  field(wrapper).vm.$emit("update:modelValue", entry.username);
  await flushPromises();
  await button(wrapper, "Добавить")!.trigger("click");
  await flushPromises();
}

describe("AccountBlacklistSection", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem("user", JSON.stringify({ username: "Артур" }));
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => vi.restoreAllMocks());

  it('ставит таблицу "Пользователь | Внесен", дату своей ячейкой, корзину в конце строки', async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-09-21T10:00:00Z"),
      anEntry("Персиваль", "2026-01-01T10:00:00Z"),
    ]);

    expect(wrapper.findAll("thead th").map((th) => th.text())).toEqual([
      "Пользователь",
      "Внесен",
      "",
    ]);
    const [first] = wrapper.findAll(".table-row");
    expect(first.findAll("td").map((td) => td.text())).toEqual([
      "Ланселот",
      "21.09.2026",
      "",
    ]);
    expect(first.get(".col-action button").attributes("aria-label")).toBe(
      "Удалить Ланселот из черного списка",
    );
    expect(wrapper.text()).not.toMatch(/внесен \d/);
  });

  it("рисует ту же таблицу, что игра и блог, со всеми столбцами на любой ширине", async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-09-21T10:00:00Z"),
    ]);

    const editor = wrapper.getComponent(BlacklistEditor);
    expect(editor.props("dated")).toBe(true);
    expect(editor.props("hint")).toBeUndefined();
    // By name: the table is a generic component, which the typed finder of
    // the test utils does not take.
    const columns = wrapper
      .getComponent({ name: "DataTable" })
      .props("columns") as Column[];
    expect(columns.map((c) => c.key)).toEqual(["user", "created", "action"]);
    for (const column of columns) {
      expect(column).not.toHaveProperty("rank");
    }
  });

  it('ставит подпись "В черном списке", под ней таблицу, поле последней строкой таблицы', async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-02-01T10:00:00Z"),
    ]);

    const captions = wrapper.findAll(".settings-caption").map((p) => p.text());
    expect(captions).toEqual(["Что делать с внесенными", "В черном списке"]);
    const caption = wrapper.findAll(".settings-caption")[1].element;
    const table = wrapper.get("table").element;
    const follows = (a: Element, b: Element) =>
      !!(a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING);
    expect(follows(caption, table)).toBe(true);
    // The last line of the table, right under the box it scrolls in.
    const line = wrapper.get(".bl-foot").element;
    expect(wrapper.get(".data-table-scroll").element.nextElementSibling).toBe(
      line,
    );
    expect(line.contains(field(wrapper).element)).toBe(true);
    expect(field(wrapper).props("placeholder")).toBe("Имя пользователя");
    expect(field(wrapper).props("includeInactive")).toBe(true);
    expect(button(wrapper, "+ Добавить")).toBeUndefined();
  });

  it("кладет новую запись первой строкой, без окна и без плашки", async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-02-01T10:00:00Z"),
      anEntry("Персиваль", "2026-01-01T10:00:00Z"),
    ]);

    await addByField(wrapper, anEntry("Мордред", "2026-03-01T10:00:00Z"));

    expect(blacklistApi.blockUser).toHaveBeenCalledWith({
      username: "Мордред",
    });
    expect(namesOnScreen(wrapper)).toEqual([
      "Мордред",
      "Ланселот",
      "Персиваль",
    ]);
    expect(field(wrapper).props("modelValue")).toBe("");
    expect(document.querySelector('[role="dialog"]')).toBeNull();
    expect(saidOutLoud()).toEqual([]);
  });

  it("пишет отказ под полем и ничего не кладет в список", async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-02-01T10:00:00Z"),
    ]);
    vi.spyOn(blacklistApi, "blockUser").mockResolvedValue(
      refused(404, "Пользователь не найден"),
    );

    field(wrapper).vm.$emit("update:modelValue", "Мордред");
    await flushPromises();
    await button(wrapper, "Добавить")!.trigger("click");
    await flushPromises();

    expect(wrapper.get(".bl-add .action-result").text()).toBe(
      "Пользователь не найден",
    );
    expect(field(wrapper).props("modelValue")).toBe("Мордред");
    expect(namesOnScreen(wrapper)).toEqual(["Ланселот"]);
  });

  it("не дает полю подсказывать себя и внесенных", async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-02-01T10:00:00Z"),
    ]);

    expect([...(field(wrapper).props("exclude") as string[])].sort()).toEqual(
      ["Артур", "Ланселот"].sort(),
    );
  });

  it("называет каждую настройку целиком, под подписью группы", async () => {
    const { wrapper } = await mountSection([]);

    expect(wrapper.get(".settings-caption").text()).toBe(
      "Что делать с внесенными",
    );
    expect(wrapper.findAll(".setting-item").map((l) => l.text())).toEqual([
      "Скрывать их комментарии",
      "Скрывать их сообщения в общем чате",
      "Скрывать их игры",
      "Скрывать их блоги",
      "Не принимать от них личные сообщения",
      "Переносить список в мои новые игры и блоги",
    ]);
  });

  // A setting whose words wrap had its box in the middle of the two lines
  // (align-items: center). It stands by the first line now, as on the guest
  // forms, and the words go on under themselves. jsdom lays nothing out, so
  // the rule is read where it is written.
  it("ставит галочку у первой строки подписи", () => {
    const style = readFileSync(
      resolve(__dirname, "AccountBlacklistSection.vue"),
      "utf8",
    ).split("<style")[1];
    const item = /\n\.setting-item\n((?:[ \t]+.*\n|\n)*)/.exec(style)?.[1];

    expect(item).toMatch(/^ {2}align-items: flex-start$/m);
    expect(item).not.toMatch(/align-items: center/);
    expect(item).toMatch(/input\n\s+margin-top: /);
  });

  it("показывает пустой список пустой строкой таблицы, а первую запись вместо нее", async () => {
    const { wrapper } = await mountSection([]);
    expect(wrapper.get(".table-empty-row").text()).toBe(
      "В черном списке пока никого нет",
    );

    await addByField(wrapper, anEntry("Мордред", "2026-03-01T10:00:00Z"));

    expect(wrapper.text()).not.toContain("В черном списке пока никого нет");
    expect(namesOnScreen(wrapper)).toEqual(["Мордред"]);
    expect(saidOutLoud()).toEqual([]);
  });

  it("не выдает неудачную загрузку за пустой список", async () => {
    setActivePinia(createPinia());
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(refused(500));
    const wrapper = mount(AccountBlacklistSection, {
      global: { stubs: { RouterLink: RouterLinkStub } },
    });
    await flushPromises();

    expect(wrapper.get("tbody .action-result").text()).toBe(
      "Не удалось загрузить черный список",
    );
    expect(wrapper.text()).not.toContain("В черном списке пока никого нет");
    // The field stays: a name put on reads the list again.
    expect(wrapper.findComponent(UserAutocomplete).exists()).toBe(true);
  });

  it("убирает запись сразу, ничего не спрашивая", async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-02-01T10:00:00Z"),
      anEntry("Персиваль", "2026-01-01T10:00:00Z"),
    ]);
    const unblock = vi
      .spyOn(blacklistApi, "unblockUser")
      .mockResolvedValue({ data: null, error: null } as never);

    await removeLinks(wrapper)[0].trigger("click");
    await flushPromises();

    expect(unblock).toHaveBeenCalledWith("Ланселот");
    expect(namesOnScreen(wrapper)).toEqual(["Персиваль"]);
    expect(document.querySelector('[role="dialog"]')).toBeNull();
    expect(saidOutLoud()).toEqual([]);
  });

  it("не шлет второй запрос, пока ждет ответа на удаление", async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-02-01T10:00:00Z"),
    ]);
    let answer: (value: unknown) => void = () => {};
    const unblock = vi.spyOn(blacklistApi, "unblockUser").mockReturnValue(
      new Promise((resolve) => {
        answer = resolve;
      }) as never,
    );

    await removeLinks(wrapper)[0].trigger("click");
    expect(removeLinks(wrapper)[0].attributes("disabled")).toBeDefined();
    await removeLinks(wrapper)[0].trigger("click");
    answer({ data: null, error: null });
    await flushPromises();

    expect(unblock).toHaveBeenCalledTimes(1);
  });

  it("пишет отказ под записью, которую не удалось удалить, и оставляет ее", async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-02-01T10:00:00Z"),
      anEntry("Персиваль", "2026-01-01T10:00:00Z"),
    ]);
    vi.spyOn(blacklistApi, "unblockUser").mockResolvedValue(refused(404));

    await removeLinks(wrapper)[1].trigger("click");
    await flushPromises();

    expect(namesOnScreen(wrapper)).toEqual(["Ланселот", "Персиваль"]);
    const line = wrapper.get(".table-details-row");
    expect(line.text()).toBe("Пользователь не удален из черного списка");
    // Under the row it is about: the second one.
    const rows = wrapper.findAll(".table-row");
    expect(rows[1].element.nextElementSibling === line.element).toBe(true);
  });

  describe("список длиннее страницы", () => {
    it("не предлагает продолжения, когда на экране весь список", async () => {
      const { wrapper } = await mountSection([
        anEntry("Ланселот", "2026-02-01T10:00:00Z"),
      ]);

      expect(button(wrapper, "Показать еще")).toBeUndefined();
    });

    it("дочитывает список с того места, где кончаются строки", async () => {
      const { wrapper, getBlacklist } = await mountSection(
        [
          anEntry("Ланселот", "2026-02-01T10:00:00Z"),
          anEntry("Персиваль", "2026-01-01T10:00:00Z"),
        ],
        3,
      );
      getBlacklist.mockResolvedValue(
        aPage([anEntry("Галахад", "2025-12-01T10:00:00Z")], 3),
      );

      await button(wrapper, "Показать еще")!.trigger("click");
      await flushPromises();

      expect(getBlacklist).toHaveBeenLastCalledWith(2, 50);
      expect(namesOnScreen(wrapper)).toEqual([
        "Ланселот",
        "Персиваль",
        "Галахад",
      ]);
      // The whole list is on screen now.
      expect(button(wrapper, "Показать еще")).toBeUndefined();
    });

    // A name added here is the head of the list on the server as well, so the
    // next page starts one further on; a name taken off, one nearer.
    it("считает добавленное и убранное здесь, когда просит следующую страницу", async () => {
      const { wrapper, getBlacklist } = await mountSection(
        [
          anEntry("Ланселот", "2026-02-01T10:00:00Z"),
          anEntry("Персиваль", "2026-01-01T10:00:00Z"),
        ],
        5,
      );
      vi.spyOn(blacklistApi, "unblockUser").mockResolvedValue({
        data: null,
        error: null,
      } as never);

      await addByField(wrapper, anEntry("Мордред", "2026-03-01T10:00:00Z"));
      await addByField(wrapper, anEntry("Тристан", "2026-03-02T10:00:00Z"));
      // Lancelot, the third row now, goes.
      await removeLinks(wrapper)[2].trigger("click");
      await flushPromises();
      expect(namesOnScreen(wrapper)).toEqual([
        "Тристан",
        "Мордред",
        "Персиваль",
      ]);

      getBlacklist.mockResolvedValue(
        aPage([anEntry("Галахад", "2025-12-01T10:00:00Z")], 6),
      );
      await button(wrapper, "Показать еще")!.trigger("click");
      await flushPromises();

      // The server holds Tristan, Mordred, Percival and three more: the next
      // page starts at the fourth.
      expect(getBlacklist).toHaveBeenLastCalledWith(3, 50);
      expect(namesOnScreen(wrapper).at(-1)).toBe("Галахад");
    });

    // Two names on the server, one on screen; a name added makes three on the
    // server and two on screen, and the rest is still offered.
    it("не прячет продолжение после добавления", async () => {
      const { wrapper } = await mountSection(
        [anEntry("Ланселот", "2026-02-01T10:00:00Z")],
        2,
      );

      await addByField(wrapper, anEntry("Мордред", "2026-03-01T10:00:00Z"));

      expect(button(wrapper, "Показать еще")).toBeDefined();
    });

    it("пишет отказ под кнопкой, если страница не пришла", async () => {
      const { wrapper, getBlacklist } = await mountSection(
        [anEntry("Ланселот", "2026-02-01T10:00:00Z")],
        2,
      );
      getBlacklist.mockResolvedValue(refused(500));

      await button(wrapper, "Показать еще")!.trigger("click");
      await flushPromises();

      expect(namesOnScreen(wrapper)).toEqual(["Ланселот"]);
      expect(wrapper.get(".more-btn + .action-result").text()).toBe(
        "Не удалось загрузить черный список",
      );
      expect(saidOutLoud()).toEqual([]);
    });
  });
});
