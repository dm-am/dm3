/**
 * @vitest-environment jsdom
 */

/**
 * The account's own blacklist: the table of the game and the blog with
 * "Внесен" in a column of its own and the field as its last line; a new name
 * first, taken off without a question; a list that did not come is not an
 * empty one; "Показать еще" from where the rows end; no toast; every setting
 * read whole.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { blacklistApi, UserAutocomplete } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { BlacklistEntry } from "@/shared/api/models/personal";
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

  it('draws the table "Пользователь | Внесен", the date in a cell of its own, the bin at the end of a row', async () => {
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

  it('stands "В черном списке" over the table, the field as its last line', async () => {
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
    // The last line of the table.
    const line = wrapper.get(".bl-foot").element;
    expect(wrapper.get(".data-table-scroll").element.nextElementSibling).toBe(
      line,
    );
    expect(line.contains(field(wrapper).element)).toBe(true);
    expect(field(wrapper).props("placeholder")).toBe("Имя пользователя");
    expect(field(wrapper).props("includeInactive")).toBe(true);
    expect(button(wrapper, "+ Добавить")).toBeUndefined();
  });

  it("puts a new entry first, with no window and no plate", async () => {
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

  it("says a refusal under the field and puts nothing on the list", async () => {
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

  it("does not let the field suggest the reader or the people listed", async () => {
    const { wrapper } = await mountSection([
      anEntry("Ланселот", "2026-02-01T10:00:00Z"),
    ]);

    expect([...(field(wrapper).props("exclude") as string[])].sort()).toEqual(
      ["Артур", "Ланселот"].sort(),
    );
  });

  it("names every setting whole, under the caption of the group", async () => {
    const { wrapper } = await mountSection([]);

    expect(wrapper.get(".settings-caption").text()).toBe(
      "Что делать с внесенными",
    );
    expect(wrapper.findAll(".setting-item").map((l) => l.text())).toEqual([
      "Скрывать их комментарии",
      "Скрывать их сообщения в глобальном чате",
      "Скрывать их игры",
      "Скрывать их блоги",
      "Не принимать от них личные сообщения",
      "Переносить список в мои новые игры и блоги",
    ]);
  });

  it("shows an empty list as the empty row of the table, and the first entry in its place", async () => {
    const { wrapper } = await mountSection([]);
    expect(wrapper.get(".table-empty-row").text()).toBe(
      "В черном списке пока никого нет",
    );

    await addByField(wrapper, anEntry("Мордред", "2026-03-01T10:00:00Z"));

    expect(wrapper.text()).not.toContain("В черном списке пока никого нет");
    expect(namesOnScreen(wrapper)).toEqual(["Мордред"]);
    expect(saidOutLoud()).toEqual([]);
  });

  it("does not pass a failed load off as an empty list", async () => {
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

  it("takes an entry off at once, asking nothing", async () => {
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

  it("sends no second request while the removal is out", async () => {
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

  it("says a refusal under the entry it could not take off, and keeps it", async () => {
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
    // Under the row it is about.
    const rows = wrapper.findAll(".table-row");
    expect(rows[1].element.nextElementSibling === line.element).toBe(true);
  });

  describe("a list longer than a page", () => {
    it("offers no more when the whole list is on screen", async () => {
      const { wrapper } = await mountSection([
        anEntry("Ланселот", "2026-02-01T10:00:00Z"),
      ]);

      expect(button(wrapper, "Показать еще")).toBeUndefined();
    });

    it("reads on from where the rows end", async () => {
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
      // The whole list is on screen.
      expect(button(wrapper, "Показать еще")).toBeUndefined();
    });

    // An addition here moves the next page one further on, a removal one nearer.
    it("counts what was added and taken off here when it asks for the next page", async () => {
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

      // The server holds Tristan, Mordred, Percival and three more: the next page starts at the fourth.
      expect(getBlacklist).toHaveBeenLastCalledWith(3, 50);
      expect(namesOnScreen(wrapper).at(-1)).toBe("Галахад");
    });

    // Two on the server and one on screen; an addition makes three and two, and the rest is still offered.
    it("keeps the offer of more after an addition", async () => {
      const { wrapper } = await mountSection(
        [anEntry("Ланселот", "2026-02-01T10:00:00Z")],
        2,
      );

      await addByField(wrapper, anEntry("Мордред", "2026-03-01T10:00:00Z"));

      expect(button(wrapper, "Показать еще")).toBeDefined();
    });

    it("says a refusal under the button when the page did not come", async () => {
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
